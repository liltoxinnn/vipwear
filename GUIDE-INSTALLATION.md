# Gestion Magasin — Guide d'installation

Ce guide s'adresse à la personne qui installe le logiciel dans le magasin.
Aucune connaissance technique n'est nécessaire.

**Durée : environ 2 minutes.**

---

## Ce dont vous avez besoin

- Un ordinateur sous **Windows 10 ou Windows 11**
- Environ **1 Go** d'espace disque
- Aucune connexion Internet

Le logiciel contient tout ce qu'il lui faut, y compris sa base de données.
Il n'y a **rien d'autre à installer**.

---

## Étape 1 — Installer le logiciel

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

**Astuce :** clic droit sur `GestionMagasin.exe` → **Envoyer vers** →
**Bureau (créer un raccourci)**.

> ⚠️ **Ne déplacez pas et ne renommez pas le dossier `pgsql`** qui se trouve
> à côté du programme : c'est la base de données du magasin.

Le premier démarrage prend une dizaine de secondes, le temps que le logiciel
prépare sa base de données. Les suivants sont immédiats.

---

## Étape 2 — Première connexion

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

## Étape 3 — Configurer votre magasin

Allez dans **Paramètres** (menu de gauche, ou touche **F12**) et renseignez :

- Le **nom de votre magasin** — il apparaîtra sur tous les tickets
- L'**adresse** et le **téléphone**
- Votre **registre du commerce**, **NIF** et **article d'imposition** —
  ces mentions sont imprimées en bas des factures
- Le **message de remerciement** en pied de ticket
- Le **délai de retour** accepté (30 jours par défaut)

Cliquez sur **Enregistrer les paramètres**.

---

## Étape 4 — Créer les comptes de vos employés

Allez dans **Utilisateurs** (**F11**) → **Nouveau compte**.

Trois profils sont disponibles :

| Rôle | Ce que la personne peut faire |
|---|---|
| **Caissier** | Vendre, encaisser, enregistrer des retours, consulter le catalogue et le stock |
| **Responsable** | Tout cela, plus : gérer les produits, les prix, le stock, les achats et consulter les rapports |
| **Administrateur** | Tout, y compris créer des comptes et modifier les paramètres |

Le mot de passe doit contenir **au moins 8 caractères, dont une lettre et un
chiffre**.

> 💡 Créez un compte **nominatif par employé**, jamais un compte partagé.
> Chaque vente est rattachée à son auteur : c'est ce qui permet le rapport
> « Ventes par employé » et le suivi en cas de litige.

---

## Étape 5 — Saisir votre catalogue

1. **Paramètres** (F12) → onglet **Marques, tailles et couleurs** →
   ajoutez vos marques (les tailles XS à XXXL et dix couleurs sont déjà là)

2. **Produits** (F3) → **Nouveau produit** → référence, nom et prix

3. Une fois le produit créé, cliquez sur **Générer les déclinaisons** :
   cochez les tailles et les couleurs, et le logiciel crée toutes les
   combinaisons d'un coup

   > Le stock se compte par **déclinaison** — « T-shirt / Noir / M » — et non
   > par produit. Un produit sans déclinaison n'apparaît donc pas en Stock.

4. Pour chaque déclinaison, saisissez le **code-barres** de l'étiquette
   (*Modifier la déclinaison*) — c'est ce qui permettra de scanner en caisse

5. **Stock** (F4) → sélectionnez un article → **Ajuster le stock** pour
   saisir les quantités présentes en rayon

---

## Sauvegarder vos données

**C'est la partie la plus importante de ce guide.**

Toutes vos données vivent sur cet ordinateur. Un disque dur qui lâche, un
vol, un dégât des eaux — et l'historique du magasin disparaît. La sauvegarde
est votre seule protection.

### Faire une sauvegarde

1. **Paramètres** (F12)
2. Descendez jusqu'à **Sauvegarde des données**
3. Cliquez sur **Sauvegarder maintenant**
4. Choisissez où enregistrer le fichier

> 📅 **Une fois par semaine au minimum**, et conservez le fichier **ailleurs
> que sur l'ordinateur du magasin** : clé USB, disque externe ou espace en
> ligne. Une sauvegarde restée sur la machine disparaît avec elle.

### Restaurer une sauvegarde

**Paramètres** (F12) → **Restaurer une sauvegarde** → choisissez le fichier.

> ⚠️ La restauration **remplace toutes les données actuelles**. Tout ce qui
> a été saisi depuis cette sauvegarde sera perdu. Le logiciel demande deux
> confirmations avant de continuer.

Fermez puis rouvrez le logiciel après une restauration.

---

## En cas de problème

### « La base de données du magasin n'a pas pu démarrer »

1. Fermez complètement le logiciel
2. Redémarrez l'ordinateur
3. Relancez le logiciel

Si le message revient, vérifiez que le dossier **`pgsql`** est toujours
présent à côté de `GestionMagasin.exe`, et que l'antivirus ne bloque pas le
logiciel.

### Le logiciel affiche une erreur inattendue

La fenêtre indique la cause à la ligne **« Détail : »**. Notez-la ou
photographiez l'écran.

Un journal technique complet est enregistré. Pour le retrouver :

1. Touche **Windows** + **R**
2. Tapez `%LOCALAPPDATA%\GestionMagasin\journaux` puis Entrée

Transmettez le fichier du jour à votre prestataire.

> Le logiciel ne se ferme pas et vos données restent intactes.

### Le logiciel est lent à démarrer

Seul le **tout premier** démarrage prend une dizaine de secondes. Si tous
les démarrages sont lents, l'antivirus analyse probablement la base de
données à chaque lancement : demandez à votre prestataire d'ajouter une
exception sur le dossier du logiciel.

---

## Où sont mes données ?

Dans votre dossier personnel Windows :

```
%LOCALAPPDATA%\GestionMagasin\donnees
```

Ce dossier ne se sauvegarde pas en le copiant : utilisez le bouton
**Sauvegarder maintenant** des Paramètres, qui produit un fichier cohérent
même pendant que le logiciel travaille.

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
