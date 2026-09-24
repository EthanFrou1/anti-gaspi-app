export type ConfettiPiece = {
  // Déplacement final depuis le centre, en points.
  dx: number;
  dy: number;
  // Rotation finale, en degrés.
  rotate: number;
  size: number;
  color: string;
  // Rond ou rectangle (bande de papier).
  round: boolean;
};

/**
 * Confettis de la célébration « Produit sauvé » : répartis tout autour de la mascotte, un peu
 * plus vers le haut. Tirage pseudo-aléatoire DÉTERMINISTE (graine fixe) : même rendu à chaque
 * fois, et fonction testable.
 */
export function confettiPieces(count: number, colors: readonly string[], seed = 42): ConfettiPiece[] {
  let state = seed;
  // Générateur congruentiel linéaire : suffisant pour disperser des confettis.
  const random = () => {
    state = (state * 1103515245 + 12345) % 2147483648;
    return state / 2147483648;
  };

  return Array.from({ length: count }, (_, index) => {
    const angle = (index / count) * Math.PI * 2 + random() * 0.4;
    const distance = 90 + random() * 70;
    return {
      dx: Math.round(Math.cos(angle) * distance),
      dy: Math.round(Math.sin(angle) * distance - 40),
      rotate: Math.round(180 + random() * 360),
      size: Math.round(8 + random() * 6),
      color: colors[index % colors.length] ?? '#FF7A1A',
      round: index % 3 === 0,
    };
  });
}
