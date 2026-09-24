import { categoryGroup, useTheme } from '@/theme';
import { categoryIcons } from './icons/categoryIcons';

type Props = {
  // Code de la catégorie de l'app (ex. « ground-meat ») ; inconnu → icône « autre ».
  code: string | undefined;
  size?: number;
  color?: string;
};

/** Icône d'une catégorie de produit, d'après son groupe dans la charte. */
export function CategoryIcon({ code, size = 24, color }: Props) {
  const theme = useTheme();
  const Icon = categoryIcons[categoryGroup(code)];
  return <Icon width={size} height={size} color={color ?? theme.colors.ink2} accessibilityElementsHidden importantForAccessibility="no" />;
}
