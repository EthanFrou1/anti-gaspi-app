import { StyleSheet, Text } from 'react-native';
import { colors, spacing } from '@/theme';

// Erreur générale d'un formulaire (celles liées à un champ s'affichent sous le champ).
export function ErrorBanner({ message }: { message: string | undefined }) {
  if (!message) {
    return null;
  }
  return (
    <Text style={styles.banner} accessibilityRole="alert">
      {message}
    </Text>
  );
}

const styles = StyleSheet.create({
  banner: {
    backgroundColor: colors.errorBackground,
    color: colors.error,
    padding: spacing.md,
    borderRadius: 8,
    fontSize: 14,
  },
});
