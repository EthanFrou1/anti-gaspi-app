import type { FC } from 'react';
import { View } from 'react-native';
import type { SvgProps } from 'react-native-svg';
import { makeStyles } from '@/theme';
import Fridge from '../../assets/brand/illustrations/decors/frigo.svg';
import EmptyFridge from '../../assets/brand/illustrations/decors/frigo-vide.svg';
import AiPot from '../../assets/brand/illustrations/decors/marmite-ia.svg';
import Pasta from '../../assets/brand/illustrations/decors/plat-pates.svg';
import MascotWelcoming from '../../assets/brand/illustrations/mascotte/mandarine-minuteur-accueillante.svg';
import MascotDelighted from '../../assets/brand/illustrations/mascotte/mandarine-minuteur-ravie.svg';

// Illustrations de la charte utilisées par l'app (réservées aux moments forts et aux états vides).
export const illustrations = {
  fridge: Fridge,
  emptyFridge: EmptyFridge,
  aiPot: AiPot,
  pasta: Pasta,
  mascotWelcoming: MascotWelcoming,
  mascotDelighted: MascotDelighted,
} satisfies Record<string, FC<SvgProps>>;

export type IllustrationName = keyof typeof illustrations;

type Props = {
  name: IllustrationName;
  size?: number;
};

/**
 * Illustration posée sur un disque clair, comme un sticker. Les dessins ont des contours encre :
 * sans ce disque, ils disparaîtraient sur le fond du mode sombre. Décorative : ignorée par
 * VoiceOver (le texte à côté dit l'essentiel).
 */
export function Illustration({ name, size = 128 }: Props) {
  const styles = useStyles();
  const Drawing = illustrations[name];
  const disc = Math.round(size * 1.25);

  return (
    <View
      style={[styles.disc, { width: disc, height: disc, borderRadius: disc / 2 }]}
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
    >
      <Drawing width={size} height={size} />
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  // Mode clair : pêche doux ; mode sombre : crème (la couleur du texte sombre de la charte).
  disc: {
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: t.scheme === 'dark' ? t.colors.ink : t.colors.primarySoft,
  },
}));
