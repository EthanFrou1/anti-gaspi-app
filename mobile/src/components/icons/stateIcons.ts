import type { FC } from 'react';
import type { SvgProps } from 'react-native-svg';
import type { BrandUrgency } from '@/theme';
import Check from '../../../assets/brand/icons/etat/a-verifier-eye.svg';
import Soon from '../../../assets/brand/icons/etat/bientot-hourglass.svg';
import Ok from '../../../assets/brand/icons/etat/ok-check-circle.svg';
import Expired from '../../../assets/brand/icons/etat/perime-x-octagon.svg';
import Urgent from '../../../assets/brand/icons/etat/urgent-flame.svg';

/** Icônes d'urgence de la charte (grille 24, trait 2 px, couleur héritée de `color`). */
export const stateIcons: Record<BrandUrgency, FC<SvgProps>> = {
  expired: Expired,
  urgent: Urgent,
  soon: Soon,
  ok: Ok,
  check: Check,
};
