// Sous Jest, un fichier .svg importé devient un composant vide (pas de transformation Metro).
const React = require('react');

module.exports = function SvgMock(props) {
  return React.createElement('SvgMock', props);
};
