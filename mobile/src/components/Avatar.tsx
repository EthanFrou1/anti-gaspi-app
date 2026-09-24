import { Text, View } from 'react-native';
import { AVATAR_TEXT_COLOR, avatarColor, avatarInitial, makeStyles, useTheme } from '@/theme';

type Props = {
  userId: string;
  displayName: string | null | undefined;
  size?: number;
  // Membres du foyer dans l'ordre d'arrivée : des couleurs différentes pour chacun.
  memberIds?: readonly string[];
};

/** Pastille ronde à l'initiale du membre, couleur tirée de la palette de la charte. */
export function Avatar({ userId, displayName, size, memberIds }: Props) {
  const theme = useTheme();
  const styles = useStyles();
  const diameter = size ?? theme.persoBadge.avatarSize;

  return (
    <View
      // Décoratif : le nom du membre est toujours écrit à côté.
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
      style={[
        styles.avatar,
        { width: diameter, height: diameter, borderRadius: diameter / 2, backgroundColor: avatarColor(userId, theme.scheme, memberIds) },
      ]}
    >
      <Text style={[styles.initial, { fontSize: Math.round(diameter * 0.5) }]} allowFontScaling={false}>
        {avatarInitial(displayName)}
      </Text>
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  avatar: { alignItems: 'center', justifyContent: 'center' },
  initial: { fontFamily: t.fonts.bodyHeavy, color: AVATAR_TEXT_COLOR },
}));
