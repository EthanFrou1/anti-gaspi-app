import type { ReactNode } from 'react';
import { Text, View } from 'react-native';
import { makeStyles } from '@/theme';
import { Illustration, type IllustrationName } from './Illustration';

type Props = {
  illustration: IllustrationName;
  title: string;
  text: string;
  // Action proposée (ex. un bouton « Aller à l'onglet Foyer »).
  children?: ReactNode;
};

/** État vide d'un écran : illustration de la charte, titre, explication, et une action éventuelle. */
export function EmptyState({ illustration, title, text, children }: Props) {
  const styles = useStyles();
  return (
    <View style={styles.container}>
      <Illustration name={illustration} />
      <Text style={styles.title} accessibilityRole="header">
        {title}
      </Text>
      <Text style={styles.text}>{text}</Text>
      {children ? <View style={styles.action}>{children}</View> : null}
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  container: { alignItems: 'center', gap: t.space.xs, paddingVertical: t.space.xl, paddingHorizontal: t.space.md },
  title: { ...t.type.title3, color: t.colors.ink, textAlign: 'center', marginTop: t.space.sm },
  text: { ...t.type.body, color: t.colors.ink2, textAlign: 'center' },
  action: { alignSelf: 'stretch', marginTop: t.space.md },
}));
