import type { ReactNode } from 'react';
import { KeyboardAvoidingView, Platform, RefreshControl, ScrollView } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { makeStyles, useTheme } from '@/theme';

type ScreenProps = {
  children: ReactNode;
  // « Tirer pour rafraîchir » : activé si onRefresh est fourni.
  refreshing?: boolean;
  onRefresh?: () => void;
  // true si l'écran a un en-tête de navigation : il gère déjà la marge du haut
  // (encoche), sans quoi elle serait comptée deux fois.
  hasHeader?: boolean;
};

/**
 * Conteneur commun des écrans : respecte les zones sûres (encoche, barre système),
 * remonte le contenu quand le clavier s'ouvre et permet de défiler.
 */
export function Screen({ children, refreshing = false, onRefresh, hasHeader = false }: ScreenProps) {
  const theme = useTheme();
  const styles = useStyles();
  return (
    <SafeAreaView
      style={styles.safeArea}
      edges={hasHeader ? ['left', 'right', 'bottom'] : ['top', 'left', 'right', 'bottom']}
    >
      <KeyboardAvoidingView
        style={styles.flex}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      >
        <ScrollView
          contentContainerStyle={styles.content}
          keyboardShouldPersistTaps="handled"
          refreshControl={
            onRefresh ? (
              <RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={theme.colors.primary} />
            ) : undefined
          }
        >
          {children}
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const useStyles = makeStyles((t) => ({
  safeArea: { flex: 1, backgroundColor: t.colors.bg },
  flex: { flex: 1 },
  content: { flexGrow: 1, padding: t.layout.screenPadding, gap: t.space.md },
}));
