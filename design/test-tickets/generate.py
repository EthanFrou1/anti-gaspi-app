"""
Tickets de caisse fictifs pour tester la lecture par l'IA (scan du ticket, V2).

Génère les images PNG et le README (articles attendus) à partir des mêmes données :
les deux ne peuvent pas diverger. Enseignes, adresses, numéros de carte et de fidélité
sont fictifs.

Usage (depuis la racine du dépôt) :
    python design/test-tickets/generate.py                  # dates relatives à aujourd'hui
    python design/test-tickets/generate.py --date 2026-09-24

Régénérer avant de tester la lecture de la date : l'API remplace une date d'achat de plus
de 30 jours par la date du jour.

Nécessite Pillow (pip install pillow) et une police à chasse fixe (Consolas, Courier New,
DejaVu Sans Mono ou Menlo).
"""

from __future__ import annotations

import argparse
import datetime as dt
from dataclasses import dataclass
from decimal import ROUND_HALF_UP, Decimal
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

OUTPUT_DIR = Path(__file__).parent
WIDTH_CHARS = 42  # largeur d'un ticket thermique de 80 mm
VAT_RATES = {1: Decimal("5.5"), 2: Decimal("20")}  # 1 : alimentaire, 2 : le reste (et l'alcool)

FONT_CANDIDATES = [
    ("C:/Windows/Fonts/consola.ttf", "C:/Windows/Fonts/consolab.ttf"),
    ("C:/Windows/Fonts/cour.ttf", "C:/Windows/Fonts/courbd.ttf"),
    ("/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSansMono-Bold.ttf"),
    ("/System/Library/Fonts/Menlo.ttc", "/System/Library/Fonts/Menlo.ttc"),
]


# ---------- Données ----------


@dataclass(frozen=True)
class Expected:
    """Ce que l'IA doit renvoyer pour une ligne alimentaire (unités de l'API)."""

    name: str
    category: str
    quantity: str
    unit: str  # Piece | Gram | Kilogram | Milliliter | Liter
    note: str = ""


@dataclass(frozen=True)
class Item:
    label: str
    price: Decimal  # prix unitaire, ou prix au kg pour un produit pesé
    count: int = 1  # « 2 x 0,89 »
    weight_kg: Decimal | None = None
    vat: int = 1
    expected: Expected | None = None  # None : ligne à écarter
    skipped_because: str = ""

    @property
    def amount(self) -> Decimal:
        raw = self.price * (self.weight_kg if self.weight_kg is not None else self.count)
        return raw.quantize(Decimal("0.01"), rounding=ROUND_HALF_UP)


@dataclass(frozen=True)
class Discount:
    label: str
    amount: Decimal  # positif : il est soustrait
    vat: int = 1


@dataclass(frozen=True)
class Ticket:
    slug: str
    purpose: str
    store: str
    address: tuple[str, ...]
    days_ago: int
    two_digit_year: bool
    time: str
    till: int
    number: int
    entries: tuple[Item | Discount, ...]
    split_in_two: bool = False


def d(value: str) -> Decimal:
    return Decimal(value)


def food(label: str, price: str, name: str, category: str, quantity: str, unit: str, *,
         count: int = 1, weight: str | None = None, vat: int = 1, note: str = "") -> Item:
    return Item(label, d(price), count, d(weight) if weight else None, vat,
                Expected(name, category, quantity, unit, note))


def other(label: str, price: str, reason: str, *, count: int = 1) -> Item:
    return Item(label, d(price), count, None, 2, None, reason)


TICKETS = (
    Ticket(
        slug="ticket-1-court",
        purpose="Ticket court et propre : libellés lisibles, un article par ligne.",
        store="SUPERETTE DU TEST",
        address=("8 place de l'Essai", "00000 TESTVILLE"),
        days_ago=0, two_digit_year=False, time="18:12", till=2, number=1047,
        entries=(
            food("BAGUETTE TRADITION", "1.20", "Baguette tradition", "bread", "1", "Piece"),
            food("LAIT DEMI-ECREME 1L", "1.05", "Lait demi-écrémé", "uht-milk", "1", "Liter",
                 note="fresh-milk acceptable (le libellé ne dit pas UHT)"),
            food("OEUFS PLEIN AIR X6", "2.35", "Œufs plein air", "eggs", "6", "Piece"),
            food("POMMES GOLDEN 1KG", "2.49", "Pommes Golden", "fruits", "1", "Kilogram"),
            food("JAMBON BLANC X4", "2.79", "Jambon blanc", "cold-cuts", "4", "Piece"),
            food("BEURRE DOUX 250G", "2.15", "Beurre doux", "butter", "250", "Gram"),
        ),
    ),
    Ticket(
        slug="ticket-2-abreviations",
        purpose="Abréviations typiques des caisses, date sur deux chiffres.",
        store="MARCHE FICTIF",
        address=("12 rue de l'Exemple", "00000 TESTVILLE"),
        days_ago=0, two_digit_year=True, time="12:37", till=5, number=8812,
        entries=(
            food("YAOURT NAT X4", "1.45", "Yaourt nature", "yogurts", "4", "Piece"),
            food("EMMENTAL RAP", "2.19", "Emmental râpé", "hard-cheese", "1", "Piece",
                 note="pas de poids sur le libellé : 1 pièce"),
            food("CRM FRAICHE 20CL", "1.09", "Crème fraîche", "cream", "200", "Milliliter"),
            food("STK HACHE 5%MG X2", "3.99", "Steak haché 5 % MG", "ground-meat", "2", "Piece"),
            food("FILET PLT X2", "5.49", "Filets de poulet", "poultry", "2", "Piece"),
            food("CHAMP PARIS 250G", "1.59", "Champignons de Paris", "vegetables", "250", "Gram"),
            food("SAL BATAVIA", "0.99", "Salade batavia", "leafy-greens", "1", "Piece"),
            food("PDT CONSO 2,5KG", "2.99", "Pommes de terre", "vegetables", "2.5", "Kilogram"),
            food("TABL CHOC NR 70% 100G", "1.39", "Chocolat noir 70 %", "dry-goods", "100", "Gram"),
            food("JUS ORG S/PULPE 1L", "1.89", "Jus d'orange sans pulpe", "drinks", "1", "Liter"),
            food("MOZZA BUF 125G", "1.79", "Mozzarella di bufala", "fresh-cheese", "125", "Gram"),
            food("LT 1/2ECR UHT 1L", "0.95", "Lait demi-écrémé UHT", "uht-milk", "6", "Liter", count=6,
                 note="6 briques de 1 L"),
        ),
    ),
    Ticket(
        slug="ticket-3-mixte-remises",
        purpose="Alimentaire et non alimentaire mélangés, remises et bon de réduction.",
        store="HYPER EXEMPLE",
        address=("Zone d'activité du Modèle", "00000 TESTVILLE"),
        days_ago=1, two_digit_year=False, time="10:05", till=14, number=30219,
        entries=(
            food("PENNE RIGATE 500G", "0.89", "Penne rigate", "dry-goods", "500", "Gram"),
            Discount("REMISE IMMEDIATE", d("0.20")),
            food("SAUCE TOMATE BASILIC 400G", "1.49", "Sauce tomate au basilic", "condiments", "400", "Gram",
                 note="canned acceptable"),
            other("LESSIVE LIQ 2L", "7.95", "entretien"),
            Discount("BON DE REDUCTION", d("1.50"), vat=2),
            food("POULET FERMIER PAC", "7.90", "Poulet fermier prêt à cuire", "poultry", "1", "Piece"),
            food("BANANES", "1.79", "Bananes", "fruits", "1.134", "Kilogram", weight="1.134"),
            other("ESSUIE-TOUT X2", "2.49", "entretien"),
            food("SODA COLA 1,5L", "1.65", "Soda au cola", "drinks", "3", "Liter", count=2,
                 note="2 bouteilles de 1,5 L"),
            Discount("LOT 2EME A -50%", d("0.83")),
            other("CROQUETTES CHAT 2KG", "5.99", "nourriture pour animaux"),
            food("CAMEMBERT AOP 250G", "2.35", "Camembert AOP", "soft-cheese", "250", "Gram"),
            other("LIQ VAISSELLE 500ML", "1.59", "entretien"),
            other("SAC CABAS REUTILISABLE", "0.50", "sac : le prompt l'ignore sans le compter dans « articles ignorés »"),
            food("VIN ROUGE 75CL", "4.50", "Vin rouge", "drinks", "750", "Milliliter", vat=2),
        ),
    ),
    Ticket(
        slug="ticket-4-long",
        purpose="Ticket long (30 articles alimentaires). L'image entière déclenche l'avertissement "
                "« Image très longue » ; les deux parties se lisent en deux scans.",
        store="EPICERIE FACTICE",
        address=("3 avenue de la Démonstration", "00000 TESTVILLE"),
        days_ago=2, two_digit_year=False, time="17:48", till=7, number=55120,
        split_in_two=True,
        entries=(
            food("LAIT DEMI ECR 1L", "0.99", "Lait demi-écrémé", "uht-milk", "6", "Liter", count=6),
            food("BAGUETTE", "1.10", "Baguette", "bread", "1", "Piece"),
            food("PAIN DE MIE 500G", "1.65", "Pain de mie", "bread", "500", "Gram"),
            food("CEREALES MUESLI 500G", "2.89", "Muesli", "dry-goods", "500", "Gram"),
            food("CONFITURE FRAISE 370G", "2.15", "Confiture de fraise", "condiments", "370", "Gram"),
            food("BEURRE DEMI SEL 250G", "2.25", "Beurre demi-sel", "butter", "250", "Gram"),
            food("OEUFS X12", "3.99", "Œufs", "eggs", "12", "Piece"),
            food("YAOURT FRUITS X8", "2.79", "Yaourts aux fruits", "yogurts", "8", "Piece"),
            food("FROMAGE BLANC 1KG", "2.45", "Fromage blanc", "fresh-cheese", "1", "Kilogram"),
            food("COMTE 200G", "3.95", "Comté", "hard-cheese", "200", "Gram"),
            food("CHEVRE BUCHE 180G", "2.29", "Bûche de chèvre", "soft-cheese", "180", "Gram"),
            food("JAMBON CRU X6 TR", "3.49", "Jambon cru", "cold-cuts", "6", "Piece", note="6 tranches"),
            food("LARDONS FUMES 2X100G", "1.99", "Lardons fumés", "cold-cuts", "200", "Gram"),
            food("ESCALOPE DINDE X2", "4.69", "Escalopes de dinde", "poultry", "2", "Piece"),
            food("CUISSE POULET X4", "4.29", "Cuisses de poulet", "poultry", "4", "Piece"),
            food("FILET CABILLAUD 400G", "7.49", "Filet de cabillaud", "fish-seafood", "400", "Gram"),
            food("THON NATUREL 3X80G", "3.59", "Thon au naturel", "canned", "240", "Gram"),
            other("DENTIFRICE 75ML", "1.99", "hygiène"),
            food("LENTILLES VERTES 500G", "1.79", "Lentilles vertes", "dry-goods", "500", "Gram"),
            food("RIZ BASMATI 1KG", "2.39", "Riz basmati", "dry-goods", "1", "Kilogram"),
            food("SPAGHETTI 500G", "0.95", "Spaghetti", "dry-goods", "1000", "Gram", count=2),
            food("HUILE OLIVE 75CL", "6.49", "Huile d'olive", "condiments", "750", "Milliliter"),
            food("MOUTARDE DIJON 210G", "1.19", "Moutarde de Dijon", "condiments", "210", "Gram"),
            food("CAROTTES 1KG", "1.29", "Carottes", "vegetables", "1", "Kilogram"),
            food("POIREAUX", "2.99", "Poireaux", "vegetables", "0.845", "Kilogram", weight="0.845"),
            food("BROCOLI", "1.79", "Brocoli", "vegetables", "1", "Piece"),
            food("SALADE ICEBERG", "1.09", "Salade iceberg", "leafy-greens", "1", "Piece"),
            food("CLEMENTINES", "2.49", "Clémentines", "fruits", "1.23", "Kilogram", weight="1.230"),
            other("PAPIER TOILETTE X12", "4.99", "hygiène"),
            food("PETITS POIS SURG 1KG", "2.19", "Petits pois surgelés", "frozen", "1", "Kilogram"),
            food("PIZZA FRAICHE 4 FROM", "2.99", "Pizza aux 4 fromages", "ready-meals", "1", "Piece"),
            food("EAU GAZEUSE 1L", "0.55", "Eau gazeuse", "drinks", "6", "Liter", count=6),
        ),
    ),
    Ticket(
        slug="ticket-5-quantites-poids",
        purpose="Quantités multiples (« 3 x ») et produits pesés (prix au kg), date sur deux chiffres.",
        store="SUPERMARCHE MODELE",
        address=("27 boulevard du Prototype", "00000 TESTVILLE"),
        days_ago=3, two_digit_year=True, time="19:21", till=3, number=7734,
        entries=(
            food("TOMATES GRAPPE", "3.29", "Tomates grappe", "vegetables", "0.742", "Kilogram", weight="0.742"),
            food("COURGETTES", "2.49", "Courgettes", "vegetables", "0.612", "Kilogram", weight="0.612"),
            food("CAROTTES VRAC", "1.49", "Carottes", "vegetables", "1.03", "Kilogram", weight="1.030"),
            food("POMMES GALA", "2.79", "Pommes Gala", "fruits", "1.264", "Kilogram", weight="1.264"),
            food("RAISIN BLANC", "3.99", "Raisin blanc", "fruits", "0.512", "Kilogram", weight="0.512"),
            food("COMTE AOP 18 MOIS", "21.90", "Comté AOP 18 mois", "hard-cheese", "0.285", "Kilogram",
                 weight="0.285", note="285 Gram acceptable"),
            food("ROTI PORC", "11.90", "Rôti de porc", "fresh-meat", "0.954", "Kilogram", weight="0.954"),
            food("SAUMON FUME 120G", "4.59", "Saumon fumé", "fish-seafood", "240", "Gram", count=2),
            food("YAOURT NAT X4", "1.45", "Yaourt nature", "yogurts", "12", "Piece", count=3,
                 note="3 paquets de 4 ; 3 Piece acceptable"),
            food("PENNE RIGATE 500G", "0.89", "Penne rigate", "dry-goods", "1500", "Gram", count=3),
            food("STEAK HACHE 15% X2", "3.49", "Steak haché 15 % MG", "ground-meat", "4", "Piece", count=2),
            food("BEURRE DOUX 250G", "2.15", "Beurre doux", "butter", "500", "Gram", count=2),
            food("EAU MINERALE 6X1,5L", "2.10", "Eau minérale", "drinks", "9", "Liter"),
            food("BIERE BLONDE 6X25CL", "4.29", "Bière blonde", "drinks", "1.5", "Liter", vat=2,
                 note="1500 Milliliter acceptable"),
            food("BAGUETTE", "1.10", "Baguette", "bread", "3", "Piece", count=3),
        ),
    ),
)


# ---------- Mise en forme du ticket ----------


def money(value: Decimal) -> str:
    return f"{value:.2f}".replace(".", ",")


def number(value: Decimal) -> str:
    return f"{value:.3f}".replace(".", ",")


def row(left: str, right: str) -> str:
    gap = WIDTH_CHARS - len(left) - len(right)
    if gap < 1:
        raise ValueError(f"Ligne trop longue pour le ticket : « {left} » + « {right} »")
    return left + " " * gap + right


def ticket_lines(ticket: Ticket, day: dt.date) -> list[tuple[str, str]]:
    """Lignes du ticket et leur style : store (titre), center, text. « item » marque le début d'un article."""
    rule = ("-" * WIDTH_CHARS, "text")
    date = day.strftime("%d/%m/%y" if ticket.two_digit_year else "%d/%m/%Y")
    lines: list[tuple[str, str]] = [(ticket.store, "store")]
    lines += [(line, "center") for line in ticket.address]
    lines += [("Tel : 00 00 00 00 00", "center"), ("SIRET 000 000 000 00000", "center"), rule]
    lines.append((row(f"{date} {ticket.time}", f"CAISSE {ticket.till:02d}  TICKET {ticket.number}"), "text"))
    lines.append(rule)

    totals = {rate: Decimal(0) for rate in VAT_RATES}
    articles = 0
    for entry in ticket.entries:
        if isinstance(entry, Discount):
            lines.append((row(f"  {entry.label}", f"-{money(entry.amount)} €  "), "text"))
            totals[entry.vat] -= entry.amount
            continue

        totals[entry.vat] += entry.amount
        amount = f"{money(entry.amount)} € {entry.vat}"
        if entry.weight_kg is not None:
            articles += 1
            lines.append((entry.label, "item"))
            lines.append((row(f"  {number(entry.weight_kg)} kg x {money(entry.price)} €/kg", amount), "text"))
        elif entry.count > 1:
            articles += entry.count
            lines.append((entry.label, "item"))
            lines.append((row(f"  {entry.count} x {money(entry.price)} €", amount), "text"))
        else:
            articles += 1
            lines.append((row(entry.label, amount), "item"))

    total = sum(totals.values(), Decimal(0))
    lines += [rule, (f"{articles} ARTICLES", "text"), (row("TOTAL", f"{money(total)} €"), "store_left")]
    lines += [(row("CB SANS CONTACT", f"{money(total)} €"), "text"), rule]
    lines.append(("TVA        HT        TVA        TTC", "text"))
    for rate_code, rate in VAT_RATES.items():
        ttc = totals[rate_code]
        if ttc == 0:
            continue
        ht = (ttc / (1 + rate / 100)).quantize(Decimal("0.01"), rounding=ROUND_HALF_UP)
        lines.append((f"{rate_code} {money(rate):>5}% {money(ht):>8} {money(ttc - ht):>9} {money(ttc):>10}", "text"))
    lines += [rule, ("CARTE BANCAIRE ************0000", "text"), ("AUTO 000000  SANS CONTACT", "text")]
    lines += [("CARTE FIDELITE 0000 0000 00", "text"), ("Points cumules : 12", "text"), rule]
    lines += [("MERCI DE VOTRE VISITE", "center"), ("A BIENTOT", "center")]
    return lines


# ---------- Rendu ----------


def load_fonts() -> tuple[ImageFont.FreeTypeFont, ImageFont.FreeTypeFont, ImageFont.FreeTypeFont]:
    for regular, bold in FONT_CANDIDATES:
        if Path(regular).exists() and Path(bold).exists():
            return (ImageFont.truetype(regular, 26), ImageFont.truetype(bold, 26), ImageFont.truetype(bold, 34))
    raise SystemExit("Aucune police à chasse fixe trouvée (Consolas, Courier New, DejaVu Sans Mono ou Menlo).")


def render(lines: list[tuple[str, str]]) -> tuple[Image.Image, list[int]]:
    """Image du ticket et ordonnée (px) du haut de chaque ligne."""
    regular, bold, title = load_fonts()
    char_width = regular.getlength("M")
    margin = 36
    line_height = 36
    width = int(margin * 2 + WIDTH_CHARS * char_width)
    tops = []
    y = margin
    for _, style in lines:
        tops.append(y)
        y += 48 if style == "store" else line_height
    image = Image.new("RGB", (width, y + margin), (255, 255, 255))
    draw = ImageDraw.Draw(image)
    ink = (28, 28, 28)
    for (text, style), top in zip(lines, tops):
        if style in ("store", "center"):
            font = title if style == "store" else regular
            draw.text(((width - font.getlength(text)) / 2, top), text, font=font, fill=ink)
        else:
            draw.text((margin, top), text, font=bold if style == "store_left" else regular, fill=ink)
    return image, tops


def split(image: Image.Image, lines: list[tuple[str, str]], tops: list[int]) -> tuple[list[Image.Image], str]:
    """Deux parties coupées net entre deux articles (sans ligne commune : pas de doublon à la
    validation). Renvoie aussi le libellé du premier article de la seconde partie."""
    items = [i for i, (_, style) in enumerate(lines) if style == "item"]
    cut = items[len(items) // 2]
    parts = [image.crop((0, 0, image.width, tops[cut])), image.crop((0, tops[cut], image.width, image.height))]
    return parts, lines[cut][0].split("  ")[0]


# ---------- README ----------


QUANTITY_UNITS = {"Piece": "pièce(s)", "Gram": "g", "Kilogram": "kg", "Milliliter": "ml", "Liter": "l"}


def readme(generated: list[tuple[Ticket, dt.date, list[str], str | None]], base_day: dt.date) -> str:
    out = [
        "# Tickets de caisse de test",
        "",
        "Tickets **fictifs** (enseignes, adresses, SIRET, cartes bancaire et de fidélité inventés) pour tester "
        "la lecture par l'IA. Images et articles attendus sont générés par `generate.py` à partir des mêmes données : "
        "ne pas modifier ce fichier à la main.",
        "",
        f"Générés le {base_day:%d/%m/%Y}. L'API remplace une date d'achat de plus de 30 jours par la date du jour : "
        "pour tester la lecture de la date, régénérer d'abord :",
        "",
        "```",
        "python design/test-tickets/generate.py",
        "```",
        "",
        "## Tester",
        "",
        "1. Envoyer les images sur le téléphone (AirDrop, e-mail…) et les enregistrer dans Photos.",
        "2. Frigo → Ticket → « Importer une image (photo ou capture d'écran) ».",
        "3. Comparer l'écran de validation avec les tableaux ci-dessous.",
        "",
        "Règles du prompt rappelées : poids ou volume écrit sur le libellé (ou prix au kg) → quantité totale en "
        "g, kg, ml ou l (« 2 x » un paquet de 500 g → 1000 g) ; sinon nombre d'articles en pièces ; lignes non "
        "alimentaires écartées (l'app affiche « N articles ignorés ») ; remises, totaux, TVA, paiement et carte de "
        "fidélité jamais extraits.",
        "",
        "Pour tous les tickets : ni magasin, ni adresse, ni `************0000`, ni `0000 0000 00` ne doivent "
        "apparaître dans le résultat.",
    ]
    for ticket, day, files, second_part_starts_at in generated:
        food_items = [e for e in ticket.entries if isinstance(e, Item) and e.expected]
        skipped = [e for e in ticket.entries if isinstance(e, Item) and not e.expected]
        out += ["", f"## {ticket.slug}", "", ticket.purpose, ""]
        out.append("Images : " + ", ".join(f"`{name}`" for name in files))
        if second_part_starts_at:
            out += ["", f"Les deux parties se suivent sans ligne commune : la partie 2 commence à "
                    f"`{second_part_starts_at}`. Elle n'a pas d'en-tête, donc pas de date : l'API y met la date du jour."]
        out += ["", f"Date d'achat attendue : **{day:%Y-%m-%d}** (imprimée "
                f"« {day.strftime('%d/%m/%y' if ticket.two_digit_year else '%d/%m/%Y')} »).", ""]
        out += [f"Produits attendus ({len(food_items)}), dans l'ordre du ticket :", "",
                "| Libellé imprimé | Nom attendu | Catégorie | Quantité | Remarque |",
                "| --- | --- | --- | --- | --- |"]
        for item in food_items:
            e = item.expected
            quantity = f"{e.quantity.replace('.', ',')} {QUANTITY_UNITS[e.unit]} (`{e.unit}`)"
            out.append(f"| `{item.label}` | {e.name} | `{e.category}` | {quantity} | {e.note} |")
        if skipped:
            out += ["", "À écarter, absents de la validation : "
                    + " ; ".join(f"`{item.label}` ({item.skipped_because})" for item in skipped) + "."]
        discounts = [e for e in ticket.entries if isinstance(e, Discount)]
        if discounts:
            out += ["", "Lignes de remise, jamais extraites : " + " ; ".join(f"`{e.label}`" for e in discounts) + "."]
    return "\n".join(out) + "\n"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--date", type=dt.date.fromisoformat, default=dt.date.today(),
                        help="date de référence (AAAA-MM-JJ), aujourd'hui par défaut")
    args = parser.parse_args()

    generated = []
    for ticket in TICKETS:
        day = args.date - dt.timedelta(days=ticket.days_ago)
        lines = ticket_lines(ticket, day)
        image, tops = render(lines)
        files = [f"{ticket.slug}.png"]
        image.save(OUTPUT_DIR / files[0], optimize=True)
        second_part_starts_at = None
        if ticket.split_in_two:
            parts, second_part_starts_at = split(image, lines, tops)
            for index, part in enumerate(parts, start=1):
                name = f"{ticket.slug}-partie-{index}.png"
                part.save(OUTPUT_DIR / name, optimize=True)
                files.append(name)
        generated.append((ticket, day, files, second_part_starts_at))
        print(f"{', '.join(files)} : {image.width} x {image.height} px")

    (OUTPUT_DIR / "README.md").write_text(readme(generated, args.date), encoding="utf-8")
    print("README.md")


if __name__ == "__main__":
    main()
