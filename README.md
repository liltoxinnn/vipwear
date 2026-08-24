# VIP MEN’S STORE — Logiciel de gestion pour magasin de vêtements

Logiciel de gestion complet pour un magasin de vêtements : catalogue et
déclinaisons, stock, caisse, achats fournisseurs, clients, retours, échanges,
rapports et gestion des employés.

L'interface est **entièrement en français** et la devise par défaut est le
**dinar algérien (DZD / DA)**.

---

## Sommaire

- [Technologies](#technologies)
- [Architecture](#architecture)
- [Installation](#installation)
- [Première connexion](#première-connexion)
- [Règles métier essentielles](#règles-métier-essentielles)
- [Rôles et permissions](#rôles-et-permissions)
- [Tests](#tests)
- [Structure du dépôt](#structure-du-dépôt)

---

## Technologies

| Domaine | Choix |
|---|---|
| Langage | C# 13 / .NET 10 |
| Interface | WPF (Windows) |
| Architecture | MVVM, services, dépôts, injection de dépendances |
| Base de données | PostgreSQL 14 ou supérieur |
| Accès aux données | Entity Framework Core 10 avec migrations |
| Documents PDF | QuestPDF (licence Community) |
| Exports Excel | ClosedXML |
| Journalisation | Serilog (fichier journalier) |
| Tests | xUnit, exécutés sur une base PostgreSQL réelle |

---

## Architecture

Le logiciel est découpé en quatre couches, plus un projet de tests.

```
GestionMagasin.Domain          Entités, énumérations, règles et contrats.
        ▲                      Aucune dépendance externe.
        │
GestionMagasin.Application     Services métier, DTOs, validation.
        ▲                      Ne connaît ni PostgreSQL ni WPF.
        │
GestionMagasin.Infrastructure  Entity Framework, dépôts, migrations,
        ▲                      documents PDF et Excel.
        │
GestionMagasin.App             Interface WPF (vues et vues-modèles).
```

Chaque couche ne dépend que de la précédente. La logique métier ne se trouve
jamais dans les fenêtres : les vues-modèles se contentent d'appeler les
services et d'afficher le résultat.

### Le stock, point central

Le stock n'est **jamais** porté par le produit mais par la **déclinaison**
(produit + taille + couleur), seule granularité qui reflète la réalité du
magasin.

Toute modification de stock passe par un service unique, `ServiceStock` :

| Opération | Effet |
|---|---|
| `AjouterStockAsync` | Augmente la quantité (réception d'achat, correction) |
| `RetirerStockAsync` | Diminue la quantité après contrôle de disponibilité |
| `RetournerStockAsync` | Réintègre un article revendable rapporté par un client |
| `AjusterStockAsync` | Fixe la quantité constatée lors d'un inventaire |
| `VerifierDisponibiliteAsync` | Indique si la quantité demandée est disponible |

Chaque appel verrouille la ligne d'inventaire concernée (`SELECT … FOR UPDATE`),
vérifie la règle métier, met à jour la quantité et crée un **mouvement de
stock** daté et motivé, le tout dans une même transaction.

---

## Installation

### Prérequis

- Windows 10 ou 11
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- PostgreSQL 14 ou supérieur

### 1. Créer la base de données

```sql
CREATE DATABASE gestionmagasin;
CREATE USER magasin WITH PASSWORD 'votre_mot_de_passe';
GRANT ALL PRIVILEGES ON DATABASE gestionmagasin TO magasin;
```

### 2. Configurer la connexion

Modifiez `src/GestionMagasin.App/appsettings.json` :

```json
{
  "ConnectionStrings": {
    "BaseDonnees": "Host=localhost;Port=5432;Database=gestionmagasin;Username=magasin;Password=votre_mot_de_passe"
  }
}
```

Pour ne pas versionner un mot de passe réel, créez plutôt un fichier
`appsettings.Local.json` à côté : il est ignoré par Git et surcharge le
fichier principal.

### 3. Compiler et lancer

```bash
dotnet restore
dotnet build -c Release
dotnet run --project src/GestionMagasin.App
```

Les migrations sont appliquées automatiquement au premier démarrage, ainsi que
l'amorçage des données de référence (permissions, rôles, tailles, couleurs,
fiche magasin).

---

## Livrer le logiciel à un magasin

Le poste du magasin n'installe **rien** : PostgreSQL est livré avec le
logiciel, qui démarre sa propre base de données.

```powershell
.\outils\telecharger-postgres.ps1   # une seule fois, environ 350 Mo
.\publier.ps1
```

L'archive obtenue dans `livraison\` est prête à être envoyée. Le magasin la
décompresse, double-clique, et travaille.

| | |
|---|---|
| Archive | environ 150 à 200 Mo |
| À installer sur le poste | rien |
| Durée d'installation | environ 2 minutes |
| Mot de passe de base de données | aucun, il est tiré au hasard |

La base écoute uniquement sur `127.0.0.1`, sur un port distinct du 5432 :
elle n'est joignable depuis aucune autre machine, et cohabite avec un
PostgreSQL déjà installé.

**Sauvegarde.** pgAdmin n'étant pas présent sur le poste, elle passe par
**Paramètres (F12) → Sauvegarder maintenant**. C'est la seule protection des
données du magasin, et le guide d'installation insiste dessus.

Pour livrer sans base de données — le magasin installe alors PostgreSQL
lui-même et saisit ses identifiants au premier démarrage :

```powershell
.\publier.ps1 -SansBaseDeDonnees
```

Le composant qui héberge la base est mis au point et éprouvé dans un dépôt
distinct, `gestionmagasin-serveur-embarque`, où douze tests le font tourner
sur un vrai serveur. Toute correction doit y être portée d'abord, puis
recopiée dans `src/GestionMagasin.ServeurEmbarque`.


## Première connexion

Un compte administrateur est créé automatiquement à la première installation :

| Identifiant | Mot de passe |
|---|---|
| `admin` | `Admin@2026` |

Le logiciel signale à la connexion que ce mot de passe doit être remplacé et
ouvre directement la fenêtre de changement. **Faites-le avant toute
utilisation réelle.**

Les mots de passe sont stockés sous forme d'empreinte PBKDF2-HMAC-SHA512
(210 000 itérations, sel aléatoire de 128 bits) : ils ne sont jamais
conservés en clair et ne peuvent pas être retrouvés, seulement réinitialisés.

---

## Règles métier essentielles

### Vente

1. Contrôle : quantité disponible ≥ quantité demandée, sinon la vente est refusée.
2. Création de la vente et de ses lignes, prix figés au moment de l'encaissement.
3. Déduction du stock et création d'un mouvement par article.
4. Enregistrement des règlements.

L'ensemble s'exécute dans **une seule transaction**. Si la moindre étape
échoue, rien n'est écrit : il ne peut jamais exister une vente sans mouvement
de stock correspondant.

### Achat

Le stock n'est **pas** modifié à la saisie de la commande. Il augmente
uniquement à la réception, totale ou partielle, de la marchandise.

### Retour

| État de l'article | Remboursement | Stock |
|---|---|---|
| Revendable | Oui | Réintégré |
| Endommagé | Oui | **Non réintégré** |

Le montant remboursé est calculé au prorata de ce que le client a réellement
payé, remise du ticket comprise.

### Échange

Le retour des articles rapportés et la vente des articles emportés sont
enregistrés dans une même transaction : il ne peut pas rester un retour sans
son remplacement, ni l'inverse.

### Protections en base

Au-delà du code, la base refuse elle-même les états incohérents :

- `quantite_disponible >= 0` — un stock négatif est impossible, même par SQL direct ;
- unicité de la combinaison produit + taille + couleur ;
- unicité du code-barres lorsqu'il est renseigné ;
- `quantite_retournee <= quantite` sur chaque ligne de vente ;
- un article endommagé ne peut jamais être marqué comme remis en stock.

### Suppressions

Les produits, clients et fournisseurs sont **désactivés**, jamais supprimés :
une vente historique ne disparaît pas parce qu'un produit a été retiré du
catalogue. Les clés étrangères en `RESTRICT` garantissent cette règle.

---

## Rôles et permissions

Trois rôles sont livrés d'origine. Leurs permissions sont modifiables et de
nouveaux rôles peuvent être créés.

| Permission | Administrateur | Responsable | Caissier |
|---|:---:|:---:|:---:|
| Consulter les produits | ✔ | ✔ | ✔ |
| Gérer les produits | ✔ | ✔ | |
| Modifier les prix | ✔ | ✔ | |
| Consulter le stock | ✔ | ✔ | ✔ |
| Gérer le stock | ✔ | ✔ | |
| Encaisser une vente | ✔ | ✔ | ✔ |
| Accorder une remise | ✔ | ✔ | |
| Annuler une vente | ✔ | ✔ | |
| Enregistrer un retour | ✔ | ✔ | ✔ |
| Gérer les achats | ✔ | ✔ | |
| Gérer les clients | ✔ | ✔ | ✔ |
| Consulter les rapports | ✔ | ✔ | |
| Gérer les utilisateurs | ✔ | | |
| Gérer les paramètres | ✔ | | |
| Consulter le journal d'audit | ✔ | | |

Les contrôles sont appliqués **dans les services**, pas seulement par le
masquage des boutons : ils ne peuvent pas être contournés.

---

## Tests

Les tests s'exécutent sur une base PostgreSQL réelle, créée et supprimée pour
chaque classe de tests. C'est indispensable : les verrous, les contraintes
`CHECK` et les transactions qui garantissent la cohérence du stock n'existent
que dans le vrai moteur.

```bash
# Une base PostgreSQL accessible est requise
dotnet test
```

Couverture des flux exigés :

| Flux | Vérification |
|---|---|
| Produit | Créer, modifier, désactiver, rechercher, refus des doublons |
| Stock | Ajouter, retirer, ajuster, historique, stock négatif impossible |
| Achat | Commande sans impact stock, réception partielle puis totale |
| Vente | Stock déduit, échec = aucune écriture, remises, paiements multiples |
| Retour | Article revendable réintégré, article endommagé non |
| Échange | Taille M → taille L, les deux stocks corrects, échec = rien |
| Permissions | Chaque rôle vérifié autorisation par autorisation |
| Rapports | Chaque montant comparé à un calcul fait à la main |
| Concurrence | Deux caisses sur le dernier article : une seule vente aboutit |

La connexion utilisée par les tests est définie dans
`tests/GestionMagasin.Tests/Socle/BaseDeTest.cs`.

---

## Structure du dépôt

```
GestionMagasin/
├── src/
│   ├── GestionMagasin.Domain/
│   │   ├── Entities/          Entités du magasin
│   │   ├── Enums/             Énumérations métier
│   │   ├── Interfaces/        Contrats de dépôts et de transactions
│   │   ├── Exceptions/        Erreurs métier, messages en français
│   │   └── Securite/          Codes de permissions et rôles
│   │
│   ├── GestionMagasin.Application/
│   │   ├── Services/          Logique métier
│   │   ├── DTOs/              Objets de transfert
│   │   ├── Validators/        Règles de validation
│   │   └── Common/            Session, formatage, périodes
│   │
│   ├── GestionMagasin.Infrastructure/
│   │   ├── Data/              Contexte EF, configurations, amorçage
│   │   ├── Repositories/      Dépôts et unité de travail
│   │   ├── Migrations/        Migrations EF Core
│   │   ├── Documents/         PDF (QuestPDF) et Excel (ClosedXML)
│   │   └── Services/          Hachage, horodatage, numérotation
│   │
│   └── GestionMagasin.App/
│       ├── Views/             Écrans et fenêtres WPF
│       ├── ViewModels/        Vues-modèles MVVM
│       ├── Resources/         Couleurs, styles, modèles de vues
│       ├── Converters/        Convertisseurs d'affichage
│       └── Services/          Navigation et boîtes de dialogue
│
└── tests/
    └── GestionMagasin.Tests/  Tests des flux métier
```

---

## Journalisation

Les journaux techniques sont écrits dans :

```
%LOCALAPPDATA%\GestionMagasin\journaux\gestionmagasin-AAAA-MM-JJ.log
```

Ils sont conservés 30 jours. Le **journal d'audit**, consultable dans
l'application par un administrateur, est distinct : il conserve les actions
métier importantes (création de produit, changement de prix, vente, retour,
mouvement de stock, connexion) avec leur auteur et leur date.

## Livrer au magasin

```powershell
.\publier.ps1
```

Le script produit `livraison\GestionMagasin-1.0.0.zip`, prêt à être envoyé.
Le poste du magasin **n'installe rien** : ni .NET, ni PostgreSQL. Il
décompresse l'archive et double-clique sur `GestionMagasin.exe`.

Le dossier `pgsql` — les binaires PostgreSQL pour Windows, environ 150 Mo —
n'est pas dans le dépôt. Le script le récupère de lui-même au premier
lancement sur un poste, puis vérifie que le dossier produit **et l'archive**
le contiennent : un paquet incomplet ne se découvrirait autrement que chez
le client.
