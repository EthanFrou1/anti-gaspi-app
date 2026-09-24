import { CameraView, useCameraPermissions, type BarcodeScanningResult } from 'expo-camera';
import { router } from 'expo-router';
import { useRef, useState } from 'react';
import { ActivityIndicator, Linking, StyleSheet, Text, View } from 'react-native';
import { Button } from '@/components/Button';
import { Screen } from '@/components/Screen';
import { FOOD_BARCODE_TYPES, readFoodBarcode } from '@/features/scan/barcode';
import { makeStyles, useTheme } from '@/theme';

/**
 * Scan d'un code-barres. Dès qu'un code valide est lu, on passe à l'écran de
 * confirmation (qui interroge l'API) : rien n'est ajouté sans validation.
 */
export default function ScanScreen() {
  const theme = useTheme();
  const styles = useStyles();
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
        <ActivityIndicator size="large" color={theme.colors.primary} />
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

const useStyles = makeStyles((t) => ({
  // L'image de la caméra reste sur fond noir, dans les deux modes.
  container: { flex: 1, backgroundColor: '#000000' },
  centered: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: t.colors.bg },
  title: { ...t.type.title3, color: t.colors.ink },
  text: { ...t.type.body, color: t.colors.ink2 },
  overlay: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: t.space.xl, padding: t.layout.screenPadding },
  frame: {
    width: '85%',
    aspectRatio: 2,
    borderWidth: 3,
    borderColor: t.palette.tangerine.base,
    borderRadius: t.radius.lg,
  },
  instructions: {
    ...t.type.bodyBold,
    color: '#FFFFFF',
    textAlign: 'center',
    backgroundColor: t.colors.scrim,
    paddingHorizontal: t.space.md,
    paddingVertical: t.space.xs,
    borderRadius: t.radius.pill,
  },
  manual: {
    position: 'absolute',
    bottom: t.space['2xl'],
    left: t.layout.screenPadding,
    right: t.layout.screenPadding,
  },
}));
