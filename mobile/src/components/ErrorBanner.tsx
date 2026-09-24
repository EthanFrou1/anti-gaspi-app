import { Text } from 'react-native';
import { makeStyles } from '@/theme';

// Erreur générale d'un formulaire (celles liées à un champ s'affichent sous le champ).
export function ErrorBanner({ message }: { message: string | undefined }) {
  const styles = useStyles();
  if (!message) {
    return null;
  }
  return (
    <Text style={styles.banner} accessibilityRole="alert">
      {message}
    </Text>
  );
}

const useStyles = makeStyles((t) => ({
  banner: {
    backgroundColor: t.scheme === 'dark' ? t.palette.framboise.darkSoft : t.palette.framboise.soft,
    color: t.scheme === 'dark' ? t.palette.framboise.darkText : t.colors.danger,
    borderRadius: t.radius.sm,
    padding: t.space.md,
    ...t.type.callout,
  },
}));
