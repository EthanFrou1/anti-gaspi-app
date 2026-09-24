import * as ImagePicker from 'expo-image-picker';
import { router, useFocusEffect } from 'expo-router';
import { useCallback, useState } from 'react';
import { Alert, Linking, Platform, Text, View } from 'react-native';
import { api } from '@/api/client';
import { asApiError } from '@/api/errors';
import type { ReceiptQuota } from '@/api/types';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Screen } from '@/components/Screen';
import { isTooNarrowToRead, prepareReceiptImage, type PreparedImage } from '@/features/receipts/image';
import { setPendingScan } from '@/features/receipts/pendingScan';
import { receiptQuotaLabel } from '@/features/receipts/rules';
import { WaitingOverlay } from '@/components/WaitingOverlay';
import { makeStyles } from '@/theme';

type Phase = 'idle' | 'preparing' | 'reading';

const PICKER_OPTIONS: ImagePicker.ImagePickerOptions = {
  mediaTypes: ['images'],
  // Recadrage libre sur Android. Sur iOS, le recadrage du système est toujours carré :
  // il couperait un ticket (long et étroit), donc la photo est envoyée entière.
  allowsEditing: Platform.OS === 'android',
  // Qualité maximale ici : la compression est faite ensuite, une seule fois (image.ts).
  quality: 1,
};

/**
 * Scan d'un ticket de caisse : photo (appareil ou galerie), préparation, lecture par l'IA.
 * Le résultat part vers l'écran de validation : rien n'est ajouté au frigo ici.
 */
export default function ReceiptScanScreen() {
  const styles = useStyles();
  const { state } = useAuth();
  const householdId = state.status === 'signedIn' ? state.user.householdId : null;
  const [quota, setQuota] = useState<ReceiptQuota | null>(null);
  const [phase, setPhase] = useState<Phase>('idle');
  const [error, setError] = useState<string | null>(null);

  const loadQuota = useCallback(async () => {
    try {
      setQuota(await api.receipts.quota());
    } catch {
      // Sans le quota, l'écran reste utilisable : l'API refusera au besoin (429).
    }
  }, []);

  useFocusEffect(
    useCallback(() => {
      void loadQuota();
    }, [loadQuota]),
  );

  async function pick(source: 'camera' | 'library') {
    if (!householdId) return;
    setError(null);

    // La galerie passe par le sélecteur du système : aucune autorisation n'est nécessaire.
    if (source === 'camera' && !(await ensureCameraPermission())) {
      return;
    }

    const result = source === 'camera'
      ? await ImagePicker.launchCameraAsync(PICKER_OPTIONS)
      : await ImagePicker.launchImageLibraryAsync(PICKER_OPTIONS);
    const asset = result.canceled ? null : result.assets[0];
    if (!asset) return;

    setPhase('preparing');
    let prepared: PreparedImage;
    try {
      prepared = await prepareReceiptImage(asset.uri);
    } catch {
      setPhase('idle');
      setError('Impossible de préparer cette image. Réessaie avec une autre.');
      return;
    }

    // Capture défilante trop longue : le texte serait illisible une fois l'image réduite.
    // On prévient AVANT l'envoi, qui consommerait un scan du quota.
    if (isTooNarrowToRead(prepared.width, prepared.height)) {
      setPhase('idle');
      if (!(await confirmNarrowImage())) return;
    }

    setPhase('reading');
    try {
      const scan = await api.receipts.scan(householdId, prepared.uri);
      setPendingScan(scan);
      // replace : le retour depuis la validation ramène au frigo, pas à cet écran.
      router.replace('/receipt/review');
    } catch (e) {
      setError(asApiError(e).message);
    } finally {
      setPhase('idle');
      void loadQuota();
    }
  }

  function confirmNarrowImage(): Promise<boolean> {
    return new Promise((resolve) => {
      Alert.alert(
        'Image très longue',
        'Une fois réduite pour la lecture, le texte risque d\'être trop petit. Découpe la capture en plusieurs parties (un scan par partie).',
        [
          { text: 'Choisir une autre image', style: 'cancel', onPress: () => resolve(false) },
          { text: 'Envoyer quand même', onPress: () => resolve(true) },
        ],
        // Android : toucher à côté ferme l'alerte, comme « Choisir une autre image ».
        { cancelable: true, onDismiss: () => resolve(false) },
      );
    });
  }

  async function ensureCameraPermission(): Promise<boolean> {
    const permission = await ImagePicker.requestCameraPermissionsAsync();
    if (permission.granted) return true;
    if (permission.canAskAgain) {
      setError('L\'appareil photo est nécessaire pour photographier le ticket. Tu peux aussi importer une image depuis ta galerie.');
    } else {
      // Refus définitif : seul l'utilisateur peut le lever, depuis les réglages du téléphone.
      Alert.alert(
        'Appareil photo désactivé',
        'Autorise l\'appareil photo dans les réglages, ou importe une image du ticket depuis ta galerie.',
        [
          { text: 'Annuler', style: 'cancel' },
          { text: 'Ouvrir les réglages', onPress: () => void Linking.openSettings() },
        ],
      );
    }
    return false;
  }

  const noQuotaLeft = quota !== null && quota.remaining <= 0;
  const busy = phase !== 'idle';

  return (
    <Screen hasHeader>
      <Text style={styles.title}>Ajoute tes courses en une photo</Text>
      <Text style={styles.text}>
        L'app reconnaît les produits alimentaires du ticket et estime leurs dates. Tu vérifies tout avant l'ajout au frigo.
      </Text>

      <View style={styles.tips}>
        <Text style={styles.tip}>• Ticket à plat, bien éclairé, en entier sur la photo.</Text>
        <Text style={styles.tip}>• Ticket très long : prends-le en plusieurs fois.</Text>
        <Text style={styles.tip}>• Ticket numérique (app de ton magasin) : fais une capture d'écran, puis importe-la.</Text>
      </View>

      <ErrorBanner message={error ?? undefined} />

      <Button title="Prendre le ticket en photo" onPress={() => void pick('camera')} disabled={noQuotaLeft || busy} />
      {/* Sélecteur du système : il ne donne à l'app que l'image choisie, sans autorisation
          d'accès à la galerie (demander cet accès ouvrirait toutes les photos du téléphone). */}
      <Button
        title="Importer une image (photo ou capture d'écran)"
        variant="secondary"
        onPress={() => void pick('library')}
        disabled={noQuotaLeft || busy}
      />
      {quota ? <Text style={styles.quota}>{receiptQuotaLabel(quota)}</Text> : null}
      {noQuotaLeft ? (
        <Button title="Saisir un produit à la main" variant="secondary" onPress={() => router.replace('/item/new')} />
      ) : null}

      <Text style={styles.privacy}>L'image sert uniquement à lire le ticket : elle n'est pas conservée.</Text>

      {/* Écran d'attente : la lecture peut prendre jusqu'à une minute. */}
      <WaitingOverlay
        visible={busy}
        title={phase === 'preparing' ? 'Préparation de la photo…' : 'Lecture du ticket…'}
        text="Ça peut prendre jusqu'à une minute."
      />
    </Screen>
  );
}

const useStyles = makeStyles((t) => ({
  title: { ...t.type.title2, color: t.colors.ink },
  text: { ...t.type.body, color: t.colors.ink2 },
  tips: { gap: t.space.xxs },
  tip: { ...t.type.body, color: t.colors.ink },
  quota: { ...t.type.caption, color: t.colors.ink3, textAlign: 'center' },
  privacy: { ...t.type.caption, color: t.colors.ink3, marginTop: t.space.md },
}));
