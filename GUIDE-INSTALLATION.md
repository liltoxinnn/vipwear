# Gestion Magasin — Guide d'installation

Ce guide s'adresse à la personne qui installe le logiciel dans le magasin.
Aucune connaissance technique particulière n'est nécessaire.

**Durée d'installation : environ 15 minutes.**

---

## Ce dont vous avez besoin

- Un ordinateur sous **Windows 10 ou Windows 11**
- Une connexion Internet, uniquement pour l'installation
- Environ **500 Mo** d'espace disque

---

## Étape 1 — Installer PostgreSQL

PostgreSQL est le programme qui conserve les données de votre magasin
(produits, stock, ventes). Il s'installe une seule fois.

1. Rendez-vous sur **https://www.postgresql.org/download/windows/**
2. Cliquez sur **Download the installer**
3. Choisissez la dernière version pour **Windows x86-64**
4. Lancez le fichier téléchargé

Pendant l'installation :

| Écran | Que faire |
|---|---|
| Installation Directory | Laissez la valeur proposée |
| Select Components | Laissez tout coché |
| Data Directory | Laissez la valeur proposée |
| **Password** | **Choisissez un mot de passe et notez-le.** Vous en aurez besoin à l'étape 3 |
| Port | Laissez **5432** |
| Locale | Laissez **Default locale** |

À la fin, si l'installateur propose **Stack Builder**, décochez la case : ce
n'est pas nécessaire.

> ⚠️ **Le mot de passe de l'étape « Password » est indispensable.**
> Notez-le dans un endroit sûr. Sans lui, le logiciel ne pourra pas
> enregistrer vos données.

---

## Étape 2 — Installer Gestion Magasin

1. Décompressez l'archive **GestionMagasin-1.0.0.zip** que vous avez reçue
2. Placez le dossier obtenu à un endroit stable, par exemple :
   ```
   C:\GestionMagasin
   ```
3. Ouvrez ce dossier et double-cliquez sur **GestionMagasin.exe**

> Si Windows affiche un avertissement bleu « Windows a protégé votre
> ordinateur », cliquez sur **Informations complémentaires** puis sur
> **Exécuter quand même**. Cet avertissement apparaît pour tout logiciel qui
> n'a pas été acheté sur le Microsoft Store.

**Astuce :** faites un clic droit sur `GestionMagasin.exe` →
**Envoyer vers** → **Bureau (créer un raccourci)** pour l'avoir sous la main.

---

## Étape 3 — Connecter le logiciel à la base de données

Au tout premier démarrage, le logiciel affiche la fenêtre
**« Configuration de la base de données »**.

Remplissez-la ainsi :

| Champ | Valeur |
|---|---|
| Serveur | `localhost` |
| Port | `5432` |
| Nom de la base de données | `gestionmagasin` |
| Utilisateur du serveur | `postgres` |
| Mot de passe du serveur | **celui noté à l'étape 1** |

Cliquez sur **Tester la connexion**.

- ✅ Message vert → cliquez sur **Enregistrer et démarrer le logiciel**
- ❌ Message rouge → voir la section *En cas de problème* en bas de ce guide

Le logiciel crée alors automatiquement la base et ses tables. Cette
opération dure quelques secondes et n'a lieu qu'une fois.

---

## Étape 4 — Première connexion

La fenêtre de connexion apparaît. Utilisez le compte livré avec le logiciel :

| Identifiant | Mot de passe |
|---|---|
| `admin` | `Admin@2026` |

Le logiciel vous demandera **immédiatement** de choisir un nouveau mot de
passe. C'est obligatoire.

> 🔒 Ce mot de passe protège l'ensemble des données de votre magasin :
> chiffre d'affaires, marges, fichier clients. Choisissez-en un que vous
> êtes seul à connaître.

---

## Étape 5 — Configurer votre magasin

Allez dans **Paramètres** (menu de gauche, ou touche **F12**) et renseignez :

- Le **nom de votre magasin** — il apparaîtra sur tous les tickets
- L'**adresse** et le **téléphone**
- Votre **registre du commerce**, **NIF** et **article d'imposition** —
  ces mentions sont imprimées en bas des factures
- Le **message de remerciement** en pied de ticket
- Le **délai de retour** accepté (30 jours par défaut)

Cliquez sur **Enregistrer les paramètres**.

---

## Étape 6 — Créer les comptes de vos employés

Allez dans **Utilisateurs** (**F11**) → **Nouveau compte**.

Trois profils sont disponibles :

| Rôle | Ce que la personne peut faire |
|---|---|
| **Caissier** | Vendre, encaisser, enregistrer des retours, consulter le catalogue et le stock |
| **Responsable** | Tout cela, plus : gérer les produits, les prix, le stock, les achats et consulter les rapports |
| **Administrateur** | Tout, y compris créer des comptes et modifier les paramètres |

> 💡 Créez un compte **nominatif par employé**, jamais un compte partagé.
> Chaque vente est rattachée à son auteur : c'est ce qui permet le rapport
> « Ventes par employé » et le suivi en cas de litige.

---

## Étape 7 — Saisir votre catalogue

1. **Paramètres** (F12) → onglet **Marques, tailles et couleurs** →
   ajoutez vos marques, et complétez les tailles et couleurs si besoin
   (les tailles XS à XXXL et dix couleurs sont déjà présentes)

2. **Produits** (F3) → **Nouveau produit** → renseignez la référence, le nom
   et les prix

3. Une fois le produit créé, cliquez sur **Générer les déclinaisons** :
   cochez les tailles et les couleurs, et le logiciel crée toutes les
   combinaisons d'un coup

4. Pour chaque déclinaison, saisissez le **code-barres** de l'étiquette
   (*Modifier la déclinaison*) — c'est ce qui permettra de scanner en caisse

5. **Stock** (F4) → sélectionnez un article → **Nouveau mouvement** pour
   saisir les quantités présentes en rayon

---

## Sauvegarder vos données

**C'est important.** Vos données vivent dans PostgreSQL, pas dans le dossier
du logiciel.

Pour créer une sauvegarde, ouvrez **pgAdmin** (installé avec PostgreSQL) :

1. Dépliez **Servers** → **PostgreSQL** → **Databases**
2. Clic droit sur **gestionmagasin** → **Backup...**
3. Choisissez un nom de fichier, par exemple
   `sauvegarde-2026-08-17.backup`
4. Cliquez sur **Backup**

> 📅 **Faites-le au moins une fois par semaine**, et conservez le fichier
> ailleurs que sur l'ordinateur du magasin : clé USB, disque externe ou
> espace en ligne. Un ordinateur peut tomber en panne ; une sauvegarde
> vous fait reprendre le travail en quelques minutes.

---

## En cas de problème

### « Le serveur ne répond pas »

PostgreSQL n'est pas démarré.

1. Touche **Windows** + **R**
2. Tapez `services.msc` puis Entrée
3. Cherchez la ligne **postgresql-x64-…**
4. Clic droit → **Démarrer**

Pour qu'il démarre tout seul à l'avenir : clic droit → **Propriétés** →
*Type de démarrage* → **Automatique**.

### « Le nom d'utilisateur ou le mot de passe est incorrect »

Le mot de passe saisi n'est pas celui choisi à l'étape 1. Si vous l'avez
perdu, PostgreSQL doit être réinstallé — vos données du magasin ne sont pas
perdues pour autant, mais faites appel à votre prestataire.

### « La base n'existe pas et n'a pas pu être créée »

Le compte utilisé n'a pas les droits suffisants. Utilisez le compte
`postgres`, qui les possède toujours.

### Le logiciel affiche une erreur inattendue

La fenêtre d'erreur indique elle-même la cause, à la ligne **« Détail : »**.
Notez-la, ou prenez une photo de l'écran.

Un journal technique complet est également enregistré. Pour le retrouver :

1. Touche **Windows** + **R**
2. Tapez `%LOCALAPPDATA%\GestionMagasin\journaux` puis Entrée

Transmettez le fichier du jour à votre prestataire : il contient le détail
nécessaire au diagnostic.

> Le logiciel ne se ferme pas et vos données restent intactes. Fermez la
> fenêtre d'erreur et poursuivez votre travail ; si l'écran concerné reste
> inutilisable, redémarrez le logiciel.

---

## Rappel des raccourcis clavier

| Touche | Écran |
|---|---|
| **F1** | Tableau de bord |
| **F2** | Caisse |
| **F3** | Produits |
| **F4** | Stock |
| **F5** | Ventes |
| **F6** | Achats |
| **F7** | Fournisseurs |
| **F8** | Clients |
| **F9** | Retours |
| **F10** | Rapports |
| **F11** | Utilisateurs |
| **F12** | Paramètres |

En caisse, le curseur reste dans le champ de scan : vous pouvez enchaîner
les articles à la douchette sans jamais toucher la souris.
