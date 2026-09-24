import { ImageManipulator, SaveFormat } from 'expo-image-manipulator';

/**
 * Plus grand côté de la photo envoyée : la résolution maximale lue par le modèle
 * (Haiku 4.5). Au-delà, l'image serait réduite par l'API d'Anthropic : on paierait
 * l'envoi de pixels ignorés.
 */
export const RECEIPT_MAX_SIDE = 1568;

// JPEG à 0,7 : le texte d'un ticket reste net, pour quelques centaines de Ko (limite API : 2 Mo).
const JPEG_QUALITY = 0.7;

/**
 * Redimensionnement à appliquer pour que le plus grand côté tienne dans maxSide,
 * proportions conservées (on ne donne qu'une dimension). null si l'image est déjà assez petite.
 */
export function resizeTarget(
  width: number,
  height: number,
  maxSide: number = RECEIPT_MAX_SIDE,
): { width: number } | { height: number } | null {
  if (Math.max(width, height) <= maxSide) {
    return null;
  }
  return width >= height ? { width: maxSide } : { height: maxSide };
}

/**
 * Prépare la photo d'un ticket avant l'envoi : redimensionnée si besoin, puis
 * réenregistrée en JPEG compressé. Renvoie l'URI du nouveau fichier (local, temporaire).
 */
export async function prepareReceiptImage(uri: string): Promise<string> {
  const context = ImageManipulator.manipulate(uri);
  // Dimensions lues sur l'image elle-même : le sélecteur ne les fournit pas toujours.
  const original = await context.renderAsync();
  const target = resizeTarget(original.width, original.height);
  const image = target ? await context.resize(target).renderAsync() : original;
  const saved = await image.saveAsync({ compress: JPEG_QUALITY, format: SaveFormat.JPEG });
  return saved.uri;
}
