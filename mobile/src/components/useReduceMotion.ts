import { useEffect, useState } from 'react';
import { AccessibilityInfo } from 'react-native';

/**
 * Réglage « Réduire les animations » du téléphone, suivi en direct (il peut changer pendant que
 * l'écran est ouvert). false tant que la valeur n'est pas connue.
 */
export function useReduceMotion(): boolean {
  const [reduceMotion, setReduceMotion] = useState(false);

  useEffect(() => {
    let active = true;
    void AccessibilityInfo.isReduceMotionEnabled().then((value) => {
      if (active) setReduceMotion(value);
    });
    const subscription = AccessibilityInfo.addEventListener('reduceMotionChanged', setReduceMotion);
    return () => {
      active = false;
      subscription.remove();
    };
  }, []);

  return reduceMotion;
}
