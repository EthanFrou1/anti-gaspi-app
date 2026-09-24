import * as ImagePicker from 'expo-image-picker';
import { router, useFocusEffect } from 'expo-router';
import { useCallback, useState } from 'react';
import { ActivityIndicator, Alert, Linking, Modal, Platform, StyleSheet, Text, View } from 'react-native';
import { api } from '@/api/client';
import { asApiError } from '@/api/errors';
import type { ReceiptQuota } from '@/api/types';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Screen } from '@/components/Screen';
import { prepareReceiptImage } from '@/features/receipts/image';
import { setPendingScan } from '@/features/receipts/pendingScan';
import { receiptQuotaLabel } from '@/features/receipts/rules';
import { colors, spacing } from '@/theme';

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
    let imageUri: string;
    try {
      imageUri = await prepareReceiptImage(asset.uri);
    } catch {
      setPhase('idle');
      setError('Impossible de préparer cette photo. Réessaie avec une autre.');
      return;
    }

    setPhase('reading');
    try {
      const scan = await api.receipts.scan(householdId, imageUri);
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

  async function ensureCameraPermission(): Promise<boolean> {
    const permission = await ImagePicker.requestCameraPermissionsAsync();
    if (permission.granted) return true;
    if (permission.canAskAgain) {
      setError('L\'appareil photo est nécessaire pour photographier le ticket. Tu peux aussi choisir une photo dans ta galerie.');
    } else {
      // Refus définitif : seul l'utilisateur peut le lever, depuis les réglages du téléphone.
      Alert.alert(
        'Appareil photo désactivé',
        'Autorise l\'appareil photo dans les réglages, ou choisis une photo du ticket dans ta galerie.',
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
      </View>

      <ErrorBanner message={error ?? undefined} />

      <Button title="Prendre le ticket en photo" onPress={() => void pick('camera')} disabled={noQuotaLeft || busy} />
      <Button
        title="Choisir une photo dans la galerie"
        variant="secondary"
        onPress={() => void pick('library')}
        disabled={noQuotaLeft || busy}
      />
      {quota ? <Text style={styles.quota}>{receiptQuotaLabel(quota)}</Text> : null}
      {noQuotaLeft ? (
        <Button title="Saisir un produit à la main" variant="secondary" onPress={() => router.replace('/item/new')} />
      ) : null}

      <Text style={styles.privacy}>La photo sert uniquement à lire le ticket : elle n'est pas conservée.</Text>

      {/* Écran d'attente : la lecture peut prendre jusqu'à une minute. */}
      <Modal visible={busy} transparent animationType="fade">
        <View style={styles.overlay}>
          <View style={styles.overlayCard}>
            <ActivityIndicator size="large" color={colors.primary} />
            <Text style={styles.overlayTitle}>{phase === 'preparing' ? 'Préparation de la photo…' : 'Lecture du ticket…'}</Text>
            <Text style={styles.overlayText}>Ça peut prendre jusqu'à une minute.</Text>
          </View>
        </View>
      </Modal>
    </Screen>
  );
}

const styles = StyleSheet.create({
  title: { fontSize: 22, fontWeight: '700', color: colors.text },
  text: { fontSize: 15, color: colors.mutedText },
  tips: { gap: spacing.xs },
  tip: { fontSize: 14, color: colors.text },
  quota: { fontSize: 13, color: colors.mutedText, textAlign: 'center' },
  privacy: { fontSize: 13, color: colors.mutedText, marginTop: spacing.md },
  overlay: { flex: 1, backgroundColor: 'rgba(0,0,0,0.4)', justifyContent: 'center', padding: spacing.lg },
  overlayCard: { backgroundColor: colors.background, borderRadius: 12, padding: spacing.lg, gap: spacing.md, alignItems: 'center' },
  overlayTitle: { fontSize: 18, fontWeight: '700', color: colors.text },
  overlayText: { fontSize: 14, color: colors.mutedText, textAlign: 'center' },
});
