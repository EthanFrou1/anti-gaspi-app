// Un fichier .svg importé est un composant React (voir metro.config.js). Les icônes dessinées
// en « currentColor » prennent la couleur passée dans la propriété color.
declare module '*.svg' {
  import type { FC } from 'react';
  import type { SvgProps } from 'react-native-svg';

  const Svg: FC<SvgProps>;
  export default Svg;
}
