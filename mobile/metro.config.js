// Configuration de Metro (le « bundler » d'Expo) : les fichiers .svg sont importés comme des
// composants React (react-native-svg-transformer) au lieu d'être traités comme des images.
// Exemple : import Flame from '../assets/brand/icons/etat/urgent-flame.svg';
const { getDefaultConfig } = require('expo/metro-config');

const config = getDefaultConfig(__dirname);

config.transformer.babelTransformerPath = require.resolve('react-native-svg-transformer/expo');
config.resolver.assetExts = config.resolver.assetExts.filter((ext) => ext !== 'svg');
config.resolver.sourceExts = [...config.resolver.sourceExts, 'svg'];

module.exports = config;
