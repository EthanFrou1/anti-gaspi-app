# Tickets de caisse de test

Tickets **fictifs** (enseignes, adresses, SIRET, cartes bancaire et de fidélité inventés) pour tester la lecture par l'IA. Images et articles attendus sont générés par `generate.py` à partir des mêmes données : ne pas modifier ce fichier à la main.

Générés le 24/09/2026. L'API remplace une date d'achat de plus de 30 jours par la date du jour : pour tester la lecture de la date, régénérer d'abord :

```
python design/test-tickets/generate.py
```

## Tester

1. Envoyer les images sur le téléphone (AirDrop, e-mail…) et les enregistrer dans Photos.
2. Frigo → Ticket → « Importer une image (photo ou capture d'écran) ».
3. Comparer l'écran de validation avec les tableaux ci-dessous.

Règles du prompt rappelées : poids ou volume écrit sur le libellé (ou prix au kg) → quantité totale en g, kg, ml ou l (« 2 x » un paquet de 500 g → 1000 g) ; sinon nombre d'articles en pièces ; lignes non alimentaires écartées (l'app affiche « N articles ignorés ») ; remises, totaux, TVA, paiement et carte de fidélité jamais extraits.

Pour tous les tickets : ni magasin, ni adresse, ni `************0000`, ni `0000 0000 00` ne doivent apparaître dans le résultat.

## ticket-1-court

Ticket court et propre : libellés lisibles, un article par ligne.

Images : `ticket-1-court.png`

Date d'achat attendue : **2026-09-24** (imprimée « 24/09/2026 »).

Produits attendus (6), dans l'ordre du ticket :

| Libellé imprimé | Nom attendu | Catégorie | Quantité | Remarque |
| --- | --- | --- | --- | --- |
| `BAGUETTE TRADITION` | Baguette tradition | `bread` | 1 pièce(s) (`Piece`) |  |
| `LAIT DEMI-ECREME 1L` | Lait demi-écrémé | `uht-milk` | 1 l (`Liter`) | fresh-milk acceptable (le libellé ne dit pas UHT) |
| `OEUFS PLEIN AIR X6` | Œufs plein air | `eggs` | 6 pièce(s) (`Piece`) |  |
| `POMMES GOLDEN 1KG` | Pommes Golden | `fruits` | 1 kg (`Kilogram`) |  |
| `JAMBON BLANC X4` | Jambon blanc | `cold-cuts` | 4 pièce(s) (`Piece`) |  |
| `BEURRE DOUX 250G` | Beurre doux | `butter` | 250 g (`Gram`) |  |

## ticket-2-abreviations

Abréviations typiques des caisses, date sur deux chiffres.

Images : `ticket-2-abreviations.png`

Date d'achat attendue : **2026-09-24** (imprimée « 24/09/26 »).

Produits attendus (12), dans l'ordre du ticket :

| Libellé imprimé | Nom attendu | Catégorie | Quantité | Remarque |
| --- | --- | --- | --- | --- |
| `YAOURT NAT X4` | Yaourt nature | `yogurts` | 4 pièce(s) (`Piece`) |  |
| `EMMENTAL RAP` | Emmental râpé | `hard-cheese` | 1 pièce(s) (`Piece`) | pas de poids sur le libellé : 1 pièce |
| `CRM FRAICHE 20CL` | Crème fraîche | `cream` | 200 ml (`Milliliter`) |  |
| `STK HACHE 5%MG X2` | Steak haché 5 % MG | `ground-meat` | 2 pièce(s) (`Piece`) |  |
| `FILET PLT X2` | Filets de poulet | `poultry` | 2 pièce(s) (`Piece`) |  |
| `CHAMP PARIS 250G` | Champignons de Paris | `vegetables` | 250 g (`Gram`) |  |
| `SAL BATAVIA` | Salade batavia | `leafy-greens` | 1 pièce(s) (`Piece`) |  |
| `PDT CONSO 2,5KG` | Pommes de terre | `vegetables` | 2,5 kg (`Kilogram`) |  |
| `TABL CHOC NR 70% 100G` | Chocolat noir 70 % | `dry-goods` | 100 g (`Gram`) |  |
| `JUS ORG S/PULPE 1L` | Jus d'orange sans pulpe | `drinks` | 1 l (`Liter`) |  |
| `MOZZA BUF 125G` | Mozzarella di bufala | `fresh-cheese` | 125 g (`Gram`) |  |
| `LT 1/2ECR UHT 1L` | Lait demi-écrémé UHT | `uht-milk` | 6 l (`Liter`) | 6 briques de 1 L |

## ticket-3-mixte-remises

Alimentaire et non alimentaire mélangés, remises et bon de réduction.

Images : `ticket-3-mixte-remises.png`

Date d'achat attendue : **2026-09-23** (imprimée « 23/09/2026 »).

Produits attendus (7), dans l'ordre du ticket :

| Libellé imprimé | Nom attendu | Catégorie | Quantité | Remarque |
| --- | --- | --- | --- | --- |
| `PENNE RIGATE 500G` | Penne rigate | `dry-goods` | 500 g (`Gram`) |  |
| `SAUCE TOMATE BASILIC 400G` | Sauce tomate au basilic | `condiments` | 400 g (`Gram`) | canned acceptable |
| `POULET FERMIER PAC` | Poulet fermier prêt à cuire | `poultry` | 1 pièce(s) (`Piece`) |  |
| `BANANES` | Bananes | `fruits` | 1,134 kg (`Kilogram`) |  |
| `SODA COLA 1,5L` | Soda au cola | `drinks` | 3 l (`Liter`) | 2 bouteilles de 1,5 L |
| `CAMEMBERT AOP 250G` | Camembert AOP | `soft-cheese` | 250 g (`Gram`) |  |
| `VIN ROUGE 75CL` | Vin rouge | `drinks` | 750 ml (`Milliliter`) |  |

À écarter, absents de la validation : `LESSIVE LIQ 2L` (entretien) ; `ESSUIE-TOUT X2` (entretien) ; `CROQUETTES CHAT 2KG` (nourriture pour animaux) ; `LIQ VAISSELLE 500ML` (entretien) ; `SAC CABAS REUTILISABLE` (sac : le prompt l'ignore sans le compter dans « articles ignorés »).

Lignes de remise, jamais extraites : `REMISE IMMEDIATE` ; `BON DE REDUCTION` ; `LOT 2EME A -50%`.

## ticket-4-long

Ticket long (30 articles alimentaires). L'image entière déclenche l'avertissement « Image très longue » ; les deux parties se lisent en deux scans.

Images : `ticket-4-long.png`, `ticket-4-long-partie-1.png`, `ticket-4-long-partie-2.png`

Les deux parties se suivent sans ligne commune : la partie 2 commence à `THON NATUREL 3X80G`. Elle n'a pas d'en-tête, donc pas de date : l'API y met la date du jour.

Date d'achat attendue : **2026-09-22** (imprimée « 22/09/2026 »).

Produits attendus (30), dans l'ordre du ticket :

| Libellé imprimé | Nom attendu | Catégorie | Quantité | Remarque |
| --- | --- | --- | --- | --- |
| `LAIT DEMI ECR 1L` | Lait demi-écrémé | `uht-milk` | 6 l (`Liter`) |  |
| `BAGUETTE` | Baguette | `bread` | 1 pièce(s) (`Piece`) |  |
| `PAIN DE MIE 500G` | Pain de mie | `bread` | 500 g (`Gram`) |  |
| `CEREALES MUESLI 500G` | Muesli | `dry-goods` | 500 g (`Gram`) |  |
| `CONFITURE FRAISE 370G` | Confiture de fraise | `condiments` | 370 g (`Gram`) |  |
| `BEURRE DEMI SEL 250G` | Beurre demi-sel | `butter` | 250 g (`Gram`) |  |
| `OEUFS X12` | Œufs | `eggs` | 12 pièce(s) (`Piece`) |  |
| `YAOURT FRUITS X8` | Yaourts aux fruits | `yogurts` | 8 pièce(s) (`Piece`) |  |
| `FROMAGE BLANC 1KG` | Fromage blanc | `fresh-cheese` | 1 kg (`Kilogram`) |  |
| `COMTE 200G` | Comté | `hard-cheese` | 200 g (`Gram`) |  |
| `CHEVRE BUCHE 180G` | Bûche de chèvre | `soft-cheese` | 180 g (`Gram`) |  |
| `JAMBON CRU X6 TR` | Jambon cru | `cold-cuts` | 6 pièce(s) (`Piece`) | 6 tranches |
| `LARDONS FUMES 2X100G` | Lardons fumés | `cold-cuts` | 200 g (`Gram`) |  |
| `ESCALOPE DINDE X2` | Escalopes de dinde | `poultry` | 2 pièce(s) (`Piece`) |  |
| `CUISSE POULET X4` | Cuisses de poulet | `poultry` | 4 pièce(s) (`Piece`) |  |
| `FILET CABILLAUD 400G` | Filet de cabillaud | `fish-seafood` | 400 g (`Gram`) |  |
| `THON NATUREL 3X80G` | Thon au naturel | `canned` | 240 g (`Gram`) |  |
| `LENTILLES VERTES 500G` | Lentilles vertes | `dry-goods` | 500 g (`Gram`) |  |
| `RIZ BASMATI 1KG` | Riz basmati | `dry-goods` | 1 kg (`Kilogram`) |  |
| `SPAGHETTI 500G` | Spaghetti | `dry-goods` | 1000 g (`Gram`) |  |
| `HUILE OLIVE 75CL` | Huile d'olive | `condiments` | 750 ml (`Milliliter`) |  |
| `MOUTARDE DIJON 210G` | Moutarde de Dijon | `condiments` | 210 g (`Gram`) |  |
| `CAROTTES 1KG` | Carottes | `vegetables` | 1 kg (`Kilogram`) |  |
| `POIREAUX` | Poireaux | `vegetables` | 0,845 kg (`Kilogram`) |  |
| `BROCOLI` | Brocoli | `vegetables` | 1 pièce(s) (`Piece`) |  |
| `SALADE ICEBERG` | Salade iceberg | `leafy-greens` | 1 pièce(s) (`Piece`) |  |
| `CLEMENTINES` | Clémentines | `fruits` | 1,23 kg (`Kilogram`) |  |
| `PETITS POIS SURG 1KG` | Petits pois surgelés | `frozen` | 1 kg (`Kilogram`) |  |
| `PIZZA FRAICHE 4 FROM` | Pizza aux 4 fromages | `ready-meals` | 1 pièce(s) (`Piece`) |  |
| `EAU GAZEUSE 1L` | Eau gazeuse | `drinks` | 6 l (`Liter`) |  |

À écarter, absents de la validation : `DENTIFRICE 75ML` (hygiène) ; `PAPIER TOILETTE X12` (hygiène).

## ticket-5-quantites-poids

Quantités multiples (« 3 x ») et produits pesés (prix au kg), date sur deux chiffres.

Images : `ticket-5-quantites-poids.png`

Date d'achat attendue : **2026-09-21** (imprimée « 21/09/26 »).

Produits attendus (15), dans l'ordre du ticket :

| Libellé imprimé | Nom attendu | Catégorie | Quantité | Remarque |
| --- | --- | --- | --- | --- |
| `TOMATES GRAPPE` | Tomates grappe | `vegetables` | 0,742 kg (`Kilogram`) |  |
| `COURGETTES` | Courgettes | `vegetables` | 0,612 kg (`Kilogram`) |  |
| `CAROTTES VRAC` | Carottes | `vegetables` | 1,03 kg (`Kilogram`) |  |
| `POMMES GALA` | Pommes Gala | `fruits` | 1,264 kg (`Kilogram`) |  |
| `RAISIN BLANC` | Raisin blanc | `fruits` | 0,512 kg (`Kilogram`) |  |
| `COMTE AOP 18 MOIS` | Comté AOP 18 mois | `hard-cheese` | 0,285 kg (`Kilogram`) | 285 Gram acceptable |
| `ROTI PORC` | Rôti de porc | `fresh-meat` | 0,954 kg (`Kilogram`) |  |
| `SAUMON FUME 120G` | Saumon fumé | `fish-seafood` | 240 g (`Gram`) |  |
| `YAOURT NAT X4` | Yaourt nature | `yogurts` | 12 pièce(s) (`Piece`) | 3 paquets de 4 ; 3 Piece acceptable |
| `PENNE RIGATE 500G` | Penne rigate | `dry-goods` | 1500 g (`Gram`) |  |
| `STEAK HACHE 15% X2` | Steak haché 15 % MG | `ground-meat` | 4 pièce(s) (`Piece`) |  |
| `BEURRE DOUX 250G` | Beurre doux | `butter` | 500 g (`Gram`) |  |
| `EAU MINERALE 6X1,5L` | Eau minérale | `drinks` | 9 l (`Liter`) |  |
| `BIERE BLONDE 6X25CL` | Bière blonde | `drinks` | 1,5 l (`Liter`) | 1500 Milliliter acceptable |
| `BAGUETTE` | Baguette | `bread` | 3 pièce(s) (`Piece`) |  |
