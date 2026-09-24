// Types de Jest chargés ici seulement : TypeScript 6 ne les inclut plus automatiquement,
// et on évite ainsi de les exposer au code de l'app.
/// <reference types="jest" />

// Faux ImageManipulator : le vrai module est natif (absent sous Jest). On vérifie les
// opérations demandées ; le réencodage lui-même est fait par iOS ou Android.
const mockSaveAsync = jest.fn();
const mockResize = jest.fn();
let mockSize = { width: 0, height: 0 };

// jest.mock est remonté en tête du fichier par Jest : les fonctions ci-dessus n'existent pas
// encore quand la fabrique s'exécute, on les appelle donc au dernier moment.
jest.mock('expo-image-manipulator', () => {
  // Taille après un resize({ width }) ou resize({ height }), proportions conservées.
  let resized: { width: number; height: number } | null = null;
  const context = {
    renderAsync: async () => {
      const size = resized ?? mockSize;
      resized = null;
      return { ...size, saveAsync: (options: unknown) => mockSaveAsync(options) };
    },
    resize: (size: { width?: number; height?: number }) => {
      mockResize(size);
      const ratio = size.width ? size.width / mockSize.width : size.height! / mockSize.height;
      resized = { width: Math.round(mockSize.width * ratio), height: Math.round(mockSize.height * ratio) };
      return context;
    },
  };
  return {
    ImageManipulator: { manipulate: () => context },
    SaveFormat: { JPEG: 'jpeg', PNG: 'png' },
  };
});

import { isTooNarrowToRead, prepareReceiptImage } from '../image';

beforeEach(() => {
  jest.clearAllMocks();
  mockSaveAsync.mockResolvedValue({ uri: 'file:///cache/ticket.jpg', width: 0, height: 0 });
});

describe('prepareReceiptImage', () => {
  it('réduit une grande photo à 1568 px sur le grand côté', async () => {
    mockSize = { width: 3024, height: 4032 };

    const prepared = await prepareReceiptImage('file:///photo.heic');

    expect(mockResize).toHaveBeenCalledWith({ height: 1568 });
    expect(prepared).toEqual({ uri: 'file:///cache/ticket.jpg', width: 1176, height: 1568 });
  });

  it('réencode TOUJOURS en JPEG compressé, même sans redimensionnement (ce qui retire EXIF et GPS)', async () => {
    mockSize = { width: 900, height: 1200 };

    await prepareReceiptImage('file:///photo.jpg');

    expect(mockResize).not.toHaveBeenCalled();
    expect(mockSaveAsync).toHaveBeenCalledWith({ compress: 0.7, format: 'jpeg' });
  });

  it('une capture d\'écran PNG (ticket numérique) suit le même traitement : réduite, puis en JPEG', async () => {
    mockSize = { width: 1179, height: 2556 };

    const prepared = await prepareReceiptImage('file:///capture.png');

    expect(mockResize).toHaveBeenCalledWith({ height: 1568 });
    expect(mockSaveAsync).toHaveBeenCalledWith({ compress: 0.7, format: 'jpeg' });
    expect(prepared.width).toBe(723);
  });
});

describe('isTooNarrowToRead', () => {
  it.each([
    [1179, 2556, 'capture d\'écran d\'iPhone'],
    [3024, 4032, 'photo d\'un ticket papier'],
    [1080, 1920, 'capture d\'écran Android'],
  ])('%d × %d (%s) : lisible', (width, height) => {
    expect(isTooNarrowToRead(width, height)).toBe(false);
  });

  it.each([
    [1179, 5112, 'capture défilante de deux écrans'],
    [800, 3200, 'ticket papier très long, recadré'],
    [5112, 1179, 'même image couchée'],
    [400, 900, 'petite image, sans réduction'],
  ])('%d × %d (%s) : trop étroite', (width, height) => {
    expect(isTooNarrowToRead(width, height)).toBe(true);
  });
});
