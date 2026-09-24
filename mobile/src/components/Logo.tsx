import { APP_NAME } from '@/config';
import { useTheme } from '@/theme';
import LockupDark from '../../assets/brand/logo/leftly-lockup-vertical-sombre.svg';
import LockupLight from '../../assets/brand/logo/leftly-lockup-vertical.svg';

// Proportions du logo vertical (viewBox 168 × 222).
const RATIO = 222 / 168;

/**
 * Logo vertical de la charte (symbole « tuile croquée » + nom), en version claire ou sombre
 * selon le mode. Le nom y est dessiné (police vectorisée) : lu comme une image par VoiceOver.
 */
export function Logo({ width = 140 }: { width?: number }) {
  const theme = useTheme();
  const Lockup = theme.scheme === 'dark' ? LockupDark : LockupLight;
  return <Lockup width={width} height={width * RATIO} accessible accessibilityRole="image" accessibilityLabel={APP_NAME} />;
}
