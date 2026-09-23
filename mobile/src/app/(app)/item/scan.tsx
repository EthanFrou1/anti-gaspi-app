import { CameraView, useCameraPermissions, type BarcodeScanningResult } from 'expo-camera';
import { router } from 'expo-router';
import { useRef, useState } from 'react';
import { ActivityIndicator, Linking, StyleSheet, Text, View } from 'react-native';
import { Button } from '@/components/Button';
import { Screen } from '@/components/Screen';
import { FOOD_BARCODE_TYPES, readFoodBarcode } from '@/features/scan/barcode';
import { colors, spacing } from '@/theme';

/**
 * Scan d'un code-barres. Dès qu'un code valide est lu, on passe à l'écran de
 * confirmation (qui interroge l'API) : rien n'est ajouté sans validation.
 */
export default function ScanScreen() {
  const [permission, requestPermission] = useCameraPermissions();
  // La caméra signale le même code plusieurs fois par seconde : on ne traite que le premier.
  const handled = useRef(false);
  const [scanning, setScanning] = useState(true);
  const [hint, setHint] = useState<string | null>(null);

  function goToManualEntry() {
    router.replace('/item/new');
  }

  if (!permission) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  if (!permission.granted) {
    return (
      <Screen hasHeader>
        <Text style={styles.title}>Accès à l'appareil photo</Text>
        <Text style={styles.text}>
          L'appareil photo sert uniquement à lire le code-barres de tes produits. Aucune photo n'est prise ni envoyée.
        </Text>
        {permission.canAskAgain ? (
          <Button title="Autoriser l'appareil photo" onPress={() => void requestPermission()} />
        ) : (
          // Refus définitif : seul l'utilisateur peut le lever, depuis les réglages du téléphone.
          <Button title="Ouvrir les réglages" onPress={() => void Linking.openSettings()} />
        )}
        <Button title="Saisir le produit à la main" variant="secondary" onPress={goToManualEntry} />
      </Screen>
    );
  }

  function handleScanned({ type, data }: BarcodeScanningResult) {
    if (handled.current) {
      return;
    }
    const barcode = readFoodBarcode(type, data);
    if (!barcode) {
      setHint('Code non reconnu : vise le code-barres du produit.');
      return;
    }
    handled.current = true;
    setScanning(false);
    // replace : le bouton « retour » de la confirmation ramène au frigo, pas à la caméra.
    router.replace({ pathname: '/item/scanned', params: { barcode } });
  }

  return (
    <View style={styles.container}>
      <CameraView
        style={StyleSheet.absoluteFill}
        facing="back"
        barcodeScannerSettings={{ barcodeTypes: FOOD_BARCODE_TYPES }}
        // undefined : désactive la détection une fois le code lu.
        onBarcodeScanned={scanning ? handleScanned : undefined}
      />
      <View style={styles.overlay} pointerEvents="box-none">
        <View style={styles.frame} />
        <Text style={styles.instructions}>{hint ?? 'Place le code-barres dans le cadre'}</Text>
        <View style={styles.manual}>
          <Button title="Saisir à la main" variant="secondary" onPress={goToManualEntry} />
        </View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#000' },
  centered: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.background },
  title: { fontSize: 20, fontWeight: '700', color: colors.text },
  text: { fontSize: 15, color: colors.mutedText },
  overlay: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: spacing.lg, padding: spacing.lg },
  frame: {
    width: '85%',
    aspectRatio: 2,
    borderWidth: 3,
    borderColor: '#FFFFFF',
    borderRadius: 12,
  },
  instructions: {
    color: '#FFFFFF',
    fontSize: 16,
    fontWeight: '600',
    textAlign: 'center',
    backgroundColor: 'rgba(0,0,0,0.5)',
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    borderRadius: 8,
  },
  manual: { position: 'absolute', bottom: spacing.xl, left: spacing.lg, right: spacing.lg, backgroundColor: colors.background, borderRadius: 8 },
});
