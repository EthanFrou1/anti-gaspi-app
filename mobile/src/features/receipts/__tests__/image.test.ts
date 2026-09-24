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
  const context = {
    renderAsync: async () => ({ ...mockSize, saveAsync: (options: unknown) => mockSaveAsync(options) }),
    resize: (size: unknown) => {
      mockResize(size);
      return context;
    },
  };
  return {
    ImageManipulator: { manipulate: () => context },
    SaveFormat: { JPEG: 'jpeg', PNG: 'png' },
  };
});

import { prepareReceiptImage } from '../image';

beforeEach(() => {
  jest.clearAllMocks();
  mockSaveAsync.mockResolvedValue({ uri: 'file:///cache/ticket.jpg', width: 0, height: 0 });
});

describe('prepareReceiptImage', () => {
  it('réduit une grande photo à 1568 px sur le grand côté', async () => {
    mockSize = { width: 3024, height: 4032 };

    const uri = await prepareReceiptImage('file:///photo.heic');

    expect(mockResize).toHaveBeenCalledWith({ height: 1568 });
    expect(uri).toBe('file:///cache/ticket.jpg');
  });

  it('réencode TOUJOURS en JPEG compressé, même sans redimensionnement (ce qui retire EXIF et GPS)', async () => {
    mockSize = { width: 900, height: 1200 };

    await prepareReceiptImage('file:///photo.jpg');

    expect(mockResize).not.toHaveBeenCalled();
    expect(mockSaveAsync).toHaveBeenCalledWith({ compress: 0.7, format: 'jpeg' });
  });
});
