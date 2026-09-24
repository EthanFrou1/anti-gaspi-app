import type { FC } from 'react';
import type { SvgProps } from 'react-native-svg';
import type { CategoryGroup } from '@/theme';
import Viande from '../../../assets/brand/icons/categorie/viande.svg';
import Volaille from '../../../assets/brand/icons/categorie/volaille.svg';
import Poisson from '../../../assets/brand/icons/categorie/poisson.svg';
import Charcuterie from '../../../assets/brand/icons/categorie/charcuterie.svg';
import Oeufs from '../../../assets/brand/icons/categorie/oeufs.svg';
import Lait from '../../../assets/brand/icons/categorie/lait.svg';
import YaourtsDesserts from '../../../assets/brand/icons/categorie/yaourts-desserts.svg';
import Fromage from '../../../assets/brand/icons/categorie/fromage.svg';
import BeurreCreme from '../../../assets/brand/icons/categorie/beurre-creme.svg';
import Fruits from '../../../assets/brand/icons/categorie/fruits.svg';
import Legumes from '../../../assets/brand/icons/categorie/legumes.svg';
import SaladeHerbes from '../../../assets/brand/icons/categorie/salade-herbes.svg';
import Pain from '../../../assets/brand/icons/categorie/pain.svg';
import Traiteur from '../../../assets/brand/icons/categorie/traiteur.svg';
import Restes from '../../../assets/brand/icons/categorie/restes.svg';
import Surgeles from '../../../assets/brand/icons/categorie/surgeles.svg';
import Conserves from '../../../assets/brand/icons/categorie/conserves.svg';
import Epicerie from '../../../assets/brand/icons/categorie/epicerie.svg';
import Sauces from '../../../assets/brand/icons/categorie/sauces.svg';
import Boissons from '../../../assets/brand/icons/categorie/boissons.svg';
import Autre from '../../../assets/brand/icons/categorie/autre.svg';

/** Icônes de catégorie de la charte (grille 24, trait 2 px, couleur héritée de `color`). */
export const categoryIcons: Record<CategoryGroup, FC<SvgProps>> = {
  viande: Viande,
  volaille: Volaille,
  poisson: Poisson,
  charcuterie: Charcuterie,
  oeufs: Oeufs,
  lait: Lait,
  'yaourts-desserts': YaourtsDesserts,
  fromage: Fromage,
  'beurre-creme': BeurreCreme,
  fruits: Fruits,
  legumes: Legumes,
  'salade-herbes': SaladeHerbes,
  pain: Pain,
  traiteur: Traiteur,
  restes: Restes,
  surgeles: Surgeles,
  conserves: Conserves,
  epicerie: Epicerie,
  sauces: Sauces,
  boissons: Boissons,
  autre: Autre,
};
