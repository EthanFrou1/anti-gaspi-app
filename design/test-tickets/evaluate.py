"""
Évalue la lecture des tickets de test par l'IA, contre l'API locale en mode Claude.

Chaque exécution CONSOMME DU CRÉDIT Anthropic (7 lectures, de l'ordre du centime chacune
avec Claude Haiku 4.5) : ne la lancer que quand c'est utile.

Prérequis :
    .\\dev.cmd -Claude -ReceiptQuota 20      # API en Development, vrai modèle, quota relevé

Usage (depuis la racine du dépôt, dans un autre terminal) :
    python design/test-tickets/evaluate.py
    python design/test-tickets/evaluate.py --only ticket-2-abreviations --save resultat.json

Le script :
  1. se connecte avec un compte de test (créé au premier lancement) et son foyer ;
  2. régénère les tickets en mémoire, datés d'aujourd'hui (generate.py), et les prépare
     comme l'app (1568 px sur le grand côté, JPEG qualité 70) ;
  3. envoie chaque ticket à l'endpoint de scan (le ticket long : entier, puis en deux parties) ;
  4. compare le résultat aux articles attendus : trouvés, oubliés, inventés, catégories,
     quantités et unités, lignes non alimentaires écartées, date d'achat ;
  5. affiche un rapport par ticket, un score global, les tokens consommés et le coût estimé
     (lus sur GET /api/dev/ai-usage, outil de développement de l'API).

Nécessite Pillow (pip install pillow). Aucune autre dépendance.
"""

from __future__ import annotations

import argparse
import datetime as dt
import difflib
import io
import json
import os
import sys
import time
import unicodedata
import urllib.error
import urllib.request
import uuid
from dataclasses import dataclass, field
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).parent))
import generate  # noqa: E402  (mêmes données que les images et le README)

DEFAULT_API = "http://127.0.0.1:5122"
EVAL_EMAIL = "evaluation-tickets@leftly.test"
# Compte local de test, sans aucune donnée réelle : mot de passe par défaut modifiable par variable d'environnement.
EVAL_PASSWORD = os.environ.get("LEFTLY_EVAL_PASSWORD", "evaluation-des-tickets-leftly")
READ_OPERATION = "Lecture de ticket"

# Tarifs Anthropic en dollars par million de tokens (vérifiés le 24/09/2026). Écriture en
# cache : 1,25 × l'entrée ; lecture en cache : 0,1 × l'entrée. À mettre à jour si le modèle change.
PRICES = {
    "claude-haiku-4-5-20251001": {"input": 1.00, "output": 5.00, "cache_write": 1.25, "cache_read": 0.10},
}
PRICES["claude-haiku-4-5"] = PRICES["claude-haiku-4-5-20251001"]

# Mêmes réglages que l'app (mobile/src/features/receipts/image.ts).
MAX_SIDE = 1568
JPEG_QUALITY = 70

MATCH_THRESHOLD = 0.6  # similarité minimale entre libellés pour apparier deux lignes


# ---------- Appels à l'API ----------


class ApiError(Exception):
    def __init__(self, status: int, body: str):
        super().__init__(f"HTTP {status} : {body[:300]}")
        self.status = status
        self.body = body


class Api:
    def __init__(self, base: str):
        self.base = base.rstrip("/")
        self.token: str | None = None

    def request(self, method: str, path: str, body: object | None = None, *,
                raw: bytes | None = None, content_type: str | None = None, timeout: int = 30) -> object:
        data = raw if raw is not None else (json.dumps(body).encode() if body is not None else None)
        headers = {"Accept": "application/json"}
        if data is not None:
            headers["Content-Type"] = content_type or "application/json"
        if self.token:
            headers["Authorization"] = f"Bearer {self.token}"
        req = urllib.request.Request(self.base + path, data=data, method=method, headers=headers)
        try:
            with urllib.request.urlopen(req, timeout=timeout) as response:
                text = response.read().decode()
                return json.loads(text) if text else None
        except urllib.error.HTTPError as error:
            raise ApiError(error.code, error.read().decode(errors="replace")) from None

    def scan(self, household_id: str, jpeg: bytes) -> dict:
        boundary = uuid.uuid4().hex
        body = (
            f"--{boundary}\r\nContent-Disposition: form-data; name=\"image\"; filename=\"ticket.jpg\"\r\n"
            "Content-Type: image/jpeg\r\n\r\n"
        ).encode() + jpeg + f"\r\n--{boundary}--\r\n".encode()
        # Jusqu'à une minute côté API (deux tentatives de 25 s) : marge confortable.
        return self.request("POST", f"/api/households/{household_id}/receipts/scan", raw=body,
                            content_type=f"multipart/form-data; boundary={boundary}", timeout=120)

    def read_usage(self) -> dict[str, dict]:
        """Tokens de lecture de tickets cumulés depuis le démarrage de l'API, par modèle."""
        usage = self.request("GET", "/api/dev/ai-usage")
        return {u["model"]: u for u in usage if u["operation"] == READ_OPERATION}


def sign_in(api: Api) -> str:
    """Connecte le compte de test (créé au besoin) et renvoie l'identifiant de son foyer."""
    try:
        auth = api.request("POST", "/api/auth/login", {"email": EVAL_EMAIL, "password": EVAL_PASSWORD})
    except ApiError as error:
        if error.status != 401:
            raise
        auth = api.request("POST", "/api/auth/register",
                           {"email": EVAL_EMAIL, "password": EVAL_PASSWORD, "displayName": "Évaluation"})
        print(f"Compte de test créé : {EVAL_EMAIL}")
    api.token = auth["accessToken"]
    household_id = auth["user"]["householdId"]
    if household_id is None:
        household_id = api.request("POST", "/api/households", {"name": "Évaluation des tickets"})["id"]
    return household_id


# ---------- Tickets ----------


@dataclass
class Scan:
    name: str
    image: Image.Image
    has_header: bool = True  # la seconde partie du ticket long n'a pas d'en-tête (pas de date)
    result: dict | None = None
    error: str | None = None
    seconds: float = 0.0
    usage: dict[str, dict] = field(default_factory=dict)  # tokens de CE scan, par modèle
    sent_size: tuple[int, int] = (0, 0)


@dataclass
class Case:
    """Un ticket évalué : une image, ou plusieurs parties dont les résultats sont réunis."""

    ticket: generate.Ticket
    title: str
    scans: list[Scan]
    purchase_day: dt.date


def build_cases(today: dt.date, only: str | None) -> list[Case]:
    cases = []
    for ticket in generate.TICKETS:
        if only and ticket.slug != only:
            continue
        day = today - dt.timedelta(days=ticket.days_ago)
        lines = generate.ticket_lines(ticket, day)
        image, tops = generate.render(lines)
        if ticket.split_in_two:
            parts, _ = generate.split(image, lines, tops)
            cases.append(Case(ticket, f"{ticket.slug} (image entière)", [Scan(f"{ticket.slug}.png", image)], day))
            cases.append(Case(ticket, f"{ticket.slug} (deux parties)", [
                Scan(f"{ticket.slug}-partie-1.png", parts[0]),
                Scan(f"{ticket.slug}-partie-2.png", parts[1], has_header=False),
            ], day))
        else:
            cases.append(Case(ticket, ticket.slug, [Scan(f"{ticket.slug}.png", image)], day))
    return cases


def prepare_jpeg(image: Image.Image) -> tuple[bytes, tuple[int, int]]:
    """Comme l'app : réduction à 1568 px sur le grand côté, puis JPEG compressé."""
    image = image.convert("RGB")
    scale = MAX_SIDE / max(image.size)
    if scale < 1:
        image = image.resize((round(image.width * scale), round(image.height * scale)), Image.LANCZOS)
    buffer = io.BytesIO()
    image.save(buffer, format="JPEG", quality=JPEG_QUALITY)
    return buffer.getvalue(), image.size


# ---------- Comparaison ----------


def normalize(text: str) -> str:
    text = unicodedata.normalize("NFKD", text).encode("ascii", "ignore").decode().upper()
    return " ".join("".join(c if c.isalnum() else " " for c in text).split())


def similarity(a: str, b: str) -> float:
    return difflib.SequenceMatcher(None, normalize(a), normalize(b)).ratio()


def to_base(quantity: float, unit: str) -> tuple[str, float]:
    """Quantité dans l'unité de base de sa dimension : 0,285 kg et 285 g sont égaux."""
    factor = {"Piece": ("pièce", 1), "Gram": ("g", 1), "Kilogram": ("g", 1000),
              "Milliliter": ("ml", 1), "Liter": ("ml", 1000)}[unit]
    return factor[0], quantity * factor[1]


def same_quantity(a: tuple[float, str], b: tuple[float, str]) -> bool:
    (dim_a, value_a), (dim_b, value_b) = to_base(*a), to_base(*b)
    return dim_a == dim_b and abs(value_a - value_b) <= max(0.005 * value_b, 0.001)


@dataclass
class Evaluation:
    expected: int = 0
    found: int = 0
    actual: int = 0
    category_ok: int = 0
    quantity_ok: int = 0
    non_food: int = 0
    non_food_excluded: int = 0
    date_ok: bool | None = None
    problems: list[str] = field(default_factory=list)


def evaluate(case: Case, categories: dict[int, str]) -> Evaluation:
    items = [e for e in case.ticket.entries if isinstance(e, generate.Item)]
    expected = [i for i in items if i.expected]
    non_food = [i for i in items if not i.expected]
    lines = [line for scan in case.scans if scan.result for line in scan.result["lines"]]
    ev = Evaluation(expected=len(expected), actual=len(lines), non_food=len(non_food))

    # Appariement glouton : les paires de libellés les plus proches d'abord.
    pairs = sorted(
        ((similarity(item.label, line["receiptText"]), i, j)
         for i, item in enumerate(expected) for j, line in enumerate(lines)),
        reverse=True)
    matched_items: dict[int, int] = {}
    used_lines: set[int] = set()
    for score, i, j in pairs:
        if score < MATCH_THRESHOLD:
            break
        if i not in matched_items and j not in used_lines:
            matched_items[i] = j
            used_lines.add(j)

    for i, item in enumerate(expected):
        e = item.expected
        if i not in matched_items:
            ev.problems.append(f"oublié      {item.label} ({e.name})")
            continue
        ev.found += 1
        line = lines[matched_items[i]]
        code = categories.get(line["categoryId"], f"id {line['categoryId']}")
        if code in {e.category, e.also_category}:
            ev.category_ok += 1
        else:
            ev.problems.append(f"catégorie   {item.label} : {code} (attendu {e.category})")
        got = (float(line["quantity"]), line["unit"])
        accepted = [(float(e.quantity), e.unit)] + ([(float(e.also_quantity[0]), e.also_quantity[1])] if e.also_quantity else [])
        if any(same_quantity(got, ok) for ok in accepted):
            ev.quantity_ok += 1
        else:
            ev.problems.append(f"quantité    {item.label} : {got[0]:g} {got[1]} (attendu {e.quantity} {e.unit})")

    for j, line in enumerate(lines):
        if j in used_lines:
            continue
        leaked = max(non_food, key=lambda n: similarity(n.label, line["receiptText"]), default=None)
        if leaked and similarity(leaked.label, line["receiptText"]) >= MATCH_THRESHOLD:
            ev.problems.append(f"non écarté  {leaked.label} ({leaked.skipped_because}) lu comme « {line['name']} »")
        else:
            ev.problems.append(f"inventé     « {line['receiptText']} » → {line['name']}")
    ev.non_food_excluded = ev.non_food - sum(1 for p in ev.problems if p.startswith("non écarté"))

    dated = [s for s in case.scans if s.has_header and s.result]
    if dated:
        result = dated[0].result
        ev.date_ok = result["purchaseDateFromReceipt"] and result["purchasedOn"] == case.purchase_day.isoformat()
        if not ev.date_ok:
            read = result["purchasedOn"] if result["purchaseDateFromReceipt"] else "non lue (date du jour)"
            ev.problems.append(f"date        {read} (attendu {case.purchase_day.isoformat()})")
    return ev


# ---------- Coût et rapport ----------


def usage_delta(before: dict[str, dict], after: dict[str, dict]) -> dict[str, dict]:
    keys = ("calls", "inputTokens", "cacheReadTokens", "cacheWriteTokens", "outputTokens")
    return {
        model: {k: now[k] - before.get(model, {}).get(k, 0) for k in keys}
        for model, now in after.items()
        if now["calls"] - before.get(model, {}).get("calls", 0) > 0
    }


def cost(usage: dict[str, dict]) -> float | None:
    total = 0.0
    for model, u in usage.items():
        price = PRICES.get(model)
        if price is None:
            return None
        total += (u["inputTokens"] * price["input"] + u["outputTokens"] * price["output"]
                  + u["cacheWriteTokens"] * price["cache_write"] + u["cacheReadTokens"] * price["cache_read"]) / 1_000_000
    return total


def tokens_label(usage: dict[str, dict]) -> str:
    inputs = sum(u["inputTokens"] + u["cacheReadTokens"] + u["cacheWriteTokens"] for u in usage.values())
    outputs = sum(u["outputTokens"] for u in usage.values())
    price = cost(usage)
    money = f"{price:.4f} $" if price is not None else "coût inconnu (modèle absent de PRICES)"
    return f"{inputs} tokens en entrée, {outputs} en sortie, {money}"


def rate(ok: int, total: int) -> float | None:
    return ok / total if total else None


def pct(value: float | None) -> str:
    return "  —" if value is None else f"{value * 100:3.0f} %"


def print_report(cases: list[Case], results: list[Evaluation | None], total_usage: dict[str, dict]) -> None:
    print()
    for case, ev in zip(cases, results):
        seconds = sum(s.seconds for s in case.scans)
        usage: dict[str, dict] = {}
        for scan in case.scans:
            for model, u in scan.usage.items():
                usage.setdefault(model, dict.fromkeys(u, 0))
                for k, v in u.items():
                    usage[model][k] += v
        sizes = " + ".join(f"{s.sent_size[0]}×{s.sent_size[1]}" for s in case.scans)
        print(f"== {case.title}")
        print(f"   {len(case.scans)} image(s) envoyée(s) ({sizes} px), {seconds:.1f} s, {tokens_label(usage)}")
        errors = [f"{s.name} : {s.error}" for s in case.scans if s.error]
        for error in errors:
            print(f"   ÉCHEC {error}")
        if ev is None:
            print()
            continue
        date = "—" if ev.date_ok is None else ("OK" if ev.date_ok else "FAUX")
        print(f"   Trouvés {ev.found}/{ev.expected} · Lignes renvoyées {ev.actual} · Catégories {ev.category_ok}/{ev.found}"
              f" · Quantités {ev.quantity_ok}/{ev.found} · Non-alimentaires écartés {ev.non_food_excluded}/{ev.non_food}"
              f" · Date {date}")
        for problem in ev.problems:
            print(f"   - {problem}")
        if not ev.problems:
            print("   Aucune erreur.")
        print()

    done = [ev for ev in results if ev is not None]
    found = sum(e.found for e in done)
    metrics = {
        "Rappel (produits trouvés)": rate(found, sum(e.expected for e in done)),
        "Précision (sans invention)": rate(found, sum(e.actual for e in done)),
        "Catégories": rate(sum(e.category_ok for e in done), found),
        "Quantités et unités": rate(sum(e.quantity_ok for e in done), found),
        "Non-alimentaires écartés": rate(sum(e.non_food_excluded for e in done), sum(e.non_food for e in done)),
        "Dates d'achat": rate(sum(1 for e in done if e.date_ok), sum(1 for e in done if e.date_ok is not None)),
    }
    print("== Résumé")
    for label, value in metrics.items():
        print(f"   {label:<28} {pct(value)}")
    scores = [v for v in metrics.values() if v is not None]
    print(f"   {'Score global (moyenne)':<28} {pct(sum(scores) / len(scores) if scores else None)}")
    calls = sum(u["calls"] for u in total_usage.values())
    print(f"   {calls} appel(s) à Claude ({', '.join(total_usage) or 'aucun'}) : {tokens_label(total_usage)}")


# ---------- Programme principal ----------


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--api", default=DEFAULT_API, help=f"adresse de l'API (défaut : {DEFAULT_API})")
    parser.add_argument("--only", choices=[t.slug for t in generate.TICKETS], help="un seul ticket")
    parser.add_argument("--save", type=Path, help="enregistre les réponses brutes de l'API (JSON), pour les réanalyser sans relancer")
    args = parser.parse_args()

    api = Api(args.api)
    cases = build_cases(dt.date.today(), args.only)
    scans = [scan for case in cases for scan in case.scans]

    # Vérifications préalables : rien n'est envoyé à l'IA si la configuration ne convient pas.
    try:
        household_id = sign_in(api)
        before = api.read_usage()
    except ApiError as error:
        if error.status == 404:
            print("GET /api/dev/ai-usage introuvable : l'API ne tourne pas en environnement Development.")
        else:
            print(f"Connexion à l'API impossible : {error}")
        return 1
    except urllib.error.URLError as error:
        print(f"API injoignable sur {args.api} ({error.reason}). Lance d'abord : .\\dev.cmd -Claude -ReceiptQuota 20")
        return 1
    quota = api.request("GET", "/api/me/receipt-quota")
    if quota["remaining"] < len(scans):
        print(f"Quota insuffisant : {quota['remaining']} scan(s) restant(s) pour {len(scans)} image(s)."
              " Relance l'API avec : .\\dev.cmd -Claude -ReceiptQuota 20")
        return 1
    categories = {c["id"]: c["code"] for c in api.request("GET", "/api/categories")}
    print(f"{len(scans)} image(s) à lire, avec le compte {EVAL_EMAIL}.")

    first = True
    for scan in scans:
        jpeg, scan.sent_size = prepare_jpeg(scan.image)
        start_usage = api.read_usage()
        start = time.monotonic()
        try:
            scan.result = api.scan(household_id, jpeg)
        except ApiError as error:
            scan.error = f"HTTP {error.status} {error.body[:200]}"
        scan.seconds = time.monotonic() - start
        scan.usage = usage_delta(start_usage, api.read_usage())
        outcome = "erreur" if scan.error else f"{len(scan.result['lines'])} ligne(s)"
        print(f"   {scan.name} : {outcome}, {scan.seconds:.1f} s")
        if first and scan.result and not scan.usage:
            # Réponse sans appel à Claude : l'API tourne avec le lecteur Fake.
            print("\nAucun token consommé : l'API est en mode Fake. Lance-la avec : .\\dev.cmd -Claude -ReceiptQuota 20")
            return 1
        first = False

    results = [evaluate(case, categories) if any(s.result for s in case.scans) else None for case in cases]
    print_report(cases, results, usage_delta(before, api.read_usage()))

    if args.save:
        payload = [{"image": s.name, "sentSize": s.sent_size, "seconds": round(s.seconds, 2), "usage": s.usage,
                    "error": s.error, "result": s.result} for s in scans]
        args.save.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"\nRéponses brutes enregistrées dans {args.save}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
