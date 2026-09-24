import { Moon, Smartphone, Sun } from '@/components/icons/lucide';
import { Screen } from '@/components/Screen';
import { SettingsRow, SettingsSection } from '@/components/SettingsList';
import { THEME_PREFERENCE_LABELS, THEME_PREFERENCES, useThemePreference } from '@/theme';

const ICONS = { system: Smartphone, light: Sun, dark: Moon } as const;

/**
 * Choix du thème : Automatique (suit le téléphone), Clair ou Sombre. Le changement est
 * immédiat et enregistré sur le téléphone.
 */
export default function ThemeSettingsScreen() {
  const { preference, setPreference } = useThemePreference();

  return (
    <Screen hasHeader>
      <SettingsSection
        title="Thème"
        footer="Automatique : l'app passe en sombre en même temps que ton téléphone (réglage Luminosité, ou programmé le soir)."
      >
        {THEME_PREFERENCES.map((option, index) => (
          <SettingsRow
            key={option}
            icon={ICONS[option]}
            label={THEME_PREFERENCE_LABELS[option]}
            selected={option === preference}
            onPress={() => setPreference(option)}
            last={index === THEME_PREFERENCES.length - 1}
          />
        ))}
      </SettingsSection>
    </Screen>
  );
}
