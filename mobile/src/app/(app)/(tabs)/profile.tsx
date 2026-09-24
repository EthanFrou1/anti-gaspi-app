import { router } from 'expo-router';
import { useState } from 'react';
import { Linking, Text, View } from 'react-native';
import { useAuth } from '@/auth/AuthContext';
import { Avatar } from '@/components/Avatar';
import { Bell, Database, House, Info, LogOut, SunMoon, Trash, UtensilsCrossed } from '@/components/icons/lucide';
import { Screen } from '@/components/Screen';
import { SettingsRow, SettingsSection } from '@/components/SettingsList';
import { APP_NAME, APP_VERSION } from '@/config';
import { makeStyles, THEME_PREFERENCE_LABELS, useThemePreference } from '@/theme';

// Source des données produit (licence ODbL : la source doit être citée).
const OPEN_FOOD_FACTS_URL = 'https://world.openfoodfacts.org';

/**
 * Profil et réglages, organisés par sections comme les Réglages d'iOS.
 */
export default function ProfileScreen() {
  const styles = useStyles();
  const { state, signOut } = useAuth();
  const { preference } = useThemePreference();
  const [signingOut, setSigningOut] = useState(false);

  if (state.status !== 'signedIn') {
    return null;
  }

  async function handleSignOut() {
    setSigningOut(true);
    await signOut();
  }

  return (
    <Screen hasHeader>
      <View style={styles.header}>
        <Avatar userId={state.user.id} displayName={state.user.displayName} size={64} />
        <View style={styles.headerText}>
          <Text style={styles.name}>{state.user.displayName}</Text>
          <Text style={styles.email}>{state.user.email}</Text>
        </View>
      </View>

      <SettingsSection title="Apparence">
        <SettingsRow
          icon={SunMoon}
          label="Thème"
          value={THEME_PREFERENCE_LABELS[preference]}
          onPress={() => router.push('/settings/theme')}
          last
        />
      </SettingsSection>

      <SettingsSection title="Mon compte">
        <SettingsRow
          icon={UtensilsCrossed}
          label="Préférences alimentaires"
          onPress={() => router.push('/preferences')}
          accessibilityHint="Régime, allergies, temps de cuisine, budget et objectif"
        />
        <SettingsRow icon={House} label="Mon foyer" onPress={() => router.navigate('/household')} last />
      </SettingsSection>

      <SettingsSection
        title="Notifications"
        footer="Un résumé chaque jour à 18 h avec les produits à consommer. Pour les couper ou masquer leur contenu sur l'écran verrouillé, passe par les réglages du téléphone."
      >
        <SettingsRow
          icon={Bell}
          label="Rappels de péremption"
          value="Réglages du téléphone"
          onPress={() => void Linking.openSettings()}
          last
        />
      </SettingsSection>

      <SettingsSection title="À propos">
        <SettingsRow icon={Info} label={`Version de ${APP_NAME}`} value={APP_VERSION} />
        <SettingsRow
          icon={Database}
          label="Données produits : Open Food Facts"
          onPress={() => void Linking.openURL(OPEN_FOOD_FACTS_URL)}
          accessibilityHint="Ouvre le site d'Open Food Facts (licence ODbL)"
          last
        />
      </SettingsSection>

      <SettingsSection title="Session">
        <SettingsRow icon={LogOut} label="Se déconnecter" onPress={() => void handleSignOut()} loading={signingOut} />
        <SettingsRow
          icon={Trash}
          label="Supprimer mon compte"
          onPress={() => router.push('/settings/delete-account')}
          destructive
          last
        />
      </SettingsSection>
    </Screen>
  );
}

const useStyles = makeStyles((t) => ({
  header: { flexDirection: 'row', alignItems: 'center', gap: t.space.md, marginBottom: t.space.xs },
  headerText: { flex: 1, gap: 2 },
  name: { ...t.type.title2, color: t.colors.ink },
  email: { ...t.type.callout, color: t.colors.ink3 },
}));
