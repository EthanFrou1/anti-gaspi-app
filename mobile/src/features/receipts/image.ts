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
 * Petit côté minimal (en px, image telle qu'envoyée) pour que le texte d'un ticket reste
 * lisible par l'IA : une quarantaine de caractères par ligne, soit une douzaine de px par
 * caractère. Une capture d'écran d'iPhone (1179 × 2556) donne 723 px : bien au-dessus.
 */
export const MIN_READABLE_SIDE = 500;

/**
 * Image trop étroite une fois réduite à 1568 px sur le grand côté : typiquement une capture
 * défilante d'un ticket numérique (1179 × 5112 → 362 px de large). L'app prévient avant
 * l'envoi, pour ne pas gaspiller un scan du quota sur une image illisible.
 */
export function isTooNarrowToRead(width: number, height: number, maxSide: number = RECEIPT_MAX_SIDE): boolean {
  const longSide = Math.max(width, height);
  const shortSide = Math.min(width, height);
  const sentShortSide = longSide > maxSide ? (shortSide * maxSide) / longSide : shortSide;
  return sentShortSide < MIN_READABLE_SIDE;
}

export type PreparedImage = { uri: string; width: number; height: number };

/**
 * Prépare l'image d'un ticket avant l'envoi (photo, ou capture d'écran d'un ticket numérique,
 * souvent en PNG) : redimensionnée si besoin, puis TOUJOURS réenregistrée en JPEG compressé.
 * Ce réencodage part des seuls pixels (jpegData sur iOS, Bitmap.compress sur Android) : les
 * métadonnées de l'image d'origine (EXIF, position GPS, modèle du téléphone) ne sont pas
 * recopiées. L'API les retire aussi de son côté.
 * Renvoie le nouveau fichier (local, temporaire) et ses dimensions.
 */
export async function prepareReceiptImage(uri: string): Promise<PreparedImage> {
  const context = ImageManipulator.manipulate(uri);
  // Dimensions lues sur l'image elle-même : le sélecteur ne les fournit pas toujours.
  const original = await context.renderAsync();
  const target = resizeTarget(original.width, original.height);
  const image = target ? await context.resize(target).renderAsync() : original;
  const saved = await image.saveAsync({ compress: JPEG_QUALITY, format: SaveFormat.JPEG });
  return { uri: saved.uri, width: image.width, height: image.height };
}
