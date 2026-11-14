# VIP MEN’S STORE — Guide d'installation

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

**Votre logo.** L'emblème de l'enseigne — le lion couronné, le nom et
« BEJAIA » — est **dessiné dans le logiciel** : il s'affiche sur l'écran de
connexion et dans le menu sans qu'aucun fichier soit nécessaire, et il reste
net à toutes les tailles.

Pour afficher **votre image exacte** à la place, voir l'étape 3 ci-dessous :
tout se fait depuis le logiciel, sans manipuler de fichier.

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
- Le **lien du QR code** imprimé en bas des tickets — votre page
  Instagram, Facebook ou Linktree. Le logiciel fabrique le code lui-même :
  collez le lien, pas une image. Laissé vide, aucun QR code n'est imprimé.
- Le **délai de retour** accepté (30 jours par défaut)

Cliquez sur **Enregistrer les paramètres**.

### Installer votre logo

Toujours dans **Paramètres**, descendez jusqu'à **Logo du magasin** :

1. Cliquez sur **Choisir une image…**
2. Sélectionnez votre logo (PNG ou JPEG)

Il remplace aussitôt l'emblème dessiné, sur l'écran de connexion comme dans
le menu — sans redémarrer le logiciel.

> Une image **carrée** donne le meilleur résultat : elle est affichée dans un
> cercle, et une image large serait rognée sur les côtés.

**Retirer le logo** revient à l'emblème dessiné.

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

1. **Paramètres** (F12) → onglet **Familles, marques, tailles et couleurs** →
   ajoutez vos marques. Les familles, les tailles et dix couleurs sont
   déjà là.

   > **Chaque famille a son système de tailles.** Une chemise se vend en
   > S, M, L ; un pantalon en 38, 40, 42 ; une chaussure en pointures.
   >
   > | Famille | Tailles proposées |
   > |---|---|
   > | T-shirts, chemises, pulls, vestes, costumes | XS à XXXL |
   > | Pantalons, jeans, shorts | 28 à 46 |
   > | Chaussures | 35 à 47 |
   > | Accessoires | Taille unique |
   >
   > C'est ce qui empêche de créer une chaussure en XXL. Vous pouvez
   > ajouter vos propres familles avec le bouton **Ajouter**.

2. **Produits** (F3) → **Nouveau produit** → **famille**, référence, nom
   et prix

   > La famille est obligatoire : c'est elle qui déterminera les tailles
   > proposées à l'étape suivante.

3. Une fois le produit créé, cliquez sur **Générer les déclinaisons** :
   cochez les tailles et les couleurs, et le logiciel crée toutes les
   combinaisons d'un coup

   > Seules les tailles de la famille sont proposées. Pour un pantalon,
   > vous verrez 28, 30, 32… et non S, M, L.

   > La **marque** se choisit dans la liste ou se tape directement : un nom
   > inconnu crée la marque à l'enregistrement du produit. La **saison**
   > propose d'emblée Printemps, Été, Automne et Hiver, et accepte toute
   > autre période propre au magasin.

   > Le stock se compte par **déclinaison** — « T-shirt / Noir / M » — et non
   > par produit. Un produit sans déclinaison n'apparaît donc pas en Stock.

4. Pour chaque déclinaison, saisissez le **code-barres** de l'étiquette
   (*Modifier la déclinaison*) — c'est ce qui permettra de scanner en caisse

5. **Stock** (F4) → cliquez sur le produit pour ouvrir ses tailles et ses
   couleurs → **cliquez sur la quantité** de la ligne à corriger et saisissez
   celle qui est réellement en rayon

   > La liste s'ouvre sur les **produits**, avec leur stock cumulé et ce qui
   > leur manque (« 3 ruptures »). Le détail des tailles s'ouvre au clic, et
   > **Retour à la liste** ramène à la vue d'ensemble.

   > Le chiffre du stock est lui-même le bouton : un clic — ou un doigt —
   > ouvre la correction de **cette** taille, sans risque de corriger la
   > ligne voisine. Le bouton **Ajuster le stock** de la barre d'outils
   > continue de fonctionner sur la ligne sélectionnée.

   > La fenêtre s'ouvre sur la quantité, déjà sélectionnée : tapez le nouveau
   > nombre, ou servez-vous des boutons **−** et **+**. Elle montre le stock
   > d'avant, celui d'après et l'écart entre les deux ; **Confirmer**
   > enregistre. Le **motif est facultatif** — quatre motifs courants sont
   > proposés d'un tapotement, et à défaut l'historique portera la mention
   > « Ajustement d'inventaire ».

---

## Imprimante de tickets

Le logiciel imprime directement sur les imprimantes thermiques de caisse de
**58 mm** ou de **80 mm** — celles vendues sous les noms POS-58, XP-58,
R58P et des dizaines d'autres. Le ticket sort à la bonne longueur, sans
fichier à enregistrer ni lecteur PDF à ouvrir.

1. Branchez l'imprimante en USB et allumez-la.
2. Installez le **pilote fourni** avec l'imprimante. Elle apparaît alors dans
   Windows sous un nom comme « POS-58 ».
3. Vérifiez qu'elle répond : Windows → **Paramètres** → **Bluetooth et
   appareils** → **Imprimantes et scanners** → l'imprimante → **Imprimer une
   page de test**.
4. Faites une vente en **Caisse** et répondez **Oui** à « Imprimer le
   ticket ? ». La première fois, le logiciel demande sur quelle imprimante
   sortent les tickets : l'imprimante de caisse est déjà sélectionnée, avec
   la largeur de son rouleau. Cliquez **Imprimer**.

Le choix est retenu : les tickets suivants partent directement, sans cette
question. Pour le modifier, ou pour essayer d'abord : **Paramètres** (F12) →
onglet **Imprimante de tickets** → **Imprimer un ticket d'essai**. Si le
ticket est correct — lisible jusqu'au bord, montants alignés à droite,
accents de « Veste légère été — Écru » bien imprimés — répondez **Oui** et
les réglages sont retenus.

> Sans le pilote du fabricant, ajoutez l'imprimante dans Windows avec le
> pilote **Generic / Text Only**, sur son port USB (USB001 en général). Le
> logiciel envoie lui-même ses commandes à l'imprimante : ce pilote suffit.

> **Microsoft Print to PDF** (et les autres imprimantes PDF, XPS, OneNote,
> fax) n'est pas une imprimante de tickets : elle ne comprend pas les
> commandes d'une imprimante de caisse et en tirerait un PDF vide, de 0 Ko,
> que le lecteur PDF déclare endommagé. Le logiciel ne la propose donc pas,
> et refuse d'y envoyer un ticket. Pour avoir les tickets en PDF en
> attendant l'imprimante, répondez **En PDF, cette fois**, ou choisissez
> **Ne plus utiliser d'imprimante** dans Paramètres.

> Le réglage appartient **au poste**, pas au magasin : il est enregistré sur
> cet ordinateur. Un second poste de caisse choisit sa propre imprimante.

Options :

| Option | Effet |
|---|---|
| Imprimer sans demander | Le ticket sort dès la vente validée, sans question |
| Imprimer le logo | Le logo du magasin en tête de ticket |
| Couper le papier | Sur une imprimante à massicot ; sans effet sinon |
| Ouvrir le tiroir-caisse | Seulement si un tiroir est branché sur l'imprimante |

Un ticket se réimprime depuis l'écran **Ventes** → la vente → **Imprimer le
ticket**. La somme reçue et la monnaie rendue n'y figurent plus : elles ne
sont connues qu'au moment de la vente.

**Le ticket ne sort pas ?** Le message affiché dit pourquoi. Les cas les plus
fréquents : l'imprimante a été renommée ou réinstallée (choisissez-la de
nouveau dans la liste), elle est éteinte ou sans papier, ou son pilote
refuse l'envoi direct (installez-la avec le pilote « Generic / Text Only »).
La vente, elle, est toujours enregistrée.

---

## Étiquettes code-barres

Chaque article reçoit **automatiquement** son code-barres dès sa création.
Les articles créés avant cette version en reçoivent un au premier démarrage.
Il suffit ensuite d'imprimer les étiquettes, de les coller sur les
vêtements, et de les scanner en caisse.

> Les codes du logiciel commencent par **20** : cette plage est réservée aux
> magasins, aucun article du commerce ne la porte. Pour garder le code
> d'origine d'un article de marque, scannez-le dans le champ **Code-barres**
> de la déclinaison : c'est lui qui sera imprimé et reconnu.

### Installer l'imprimante d'étiquettes

1. Branchez l'imprimante et installez le **pilote fourni** par le fabricant.
2. Dans Windows → **Imprimantes et scanners** → l'imprimante → **Options
   d'impression**, choisissez la **taille de vos étiquettes**,
   **40 × 20 mm** comme dans le logiciel, et le papier « étiquettes avec
   espace » (*gap*).
3. Si l'imprimante a un bouton d'avance, faites-lui repérer les étiquettes
   (souvent : maintenir le bouton jusqu'à ce qu'elle avance de deux ou trois
   étiquettes).

### Sur l'imprimante de tickets (POS-58, R58P…)

Une imprimante de tickets garnie d'un rouleau d'étiquettes de 40 × 20 mm
convient aussi. Elle ne voit pas les étiquettes : le logiciel les lui
envoie directement, chacune exactement à sa place sur le rouleau. Il la
reconnaît à son nom (« Imprimante de tickets 58 mm » dans la fenêtre des
étiquettes) ; ses options dans Windows n'ont pas à être touchées.

La position se règle **une seule fois** :

1. Placez le rouleau bien au milieu de l'imprimante.
2. Fenêtre des étiquettes → **Imprimer le réglage (5 étiquettes)**. Chacune
   porte un cadre qui doit suivre exactement ses bords.
3. Le cadre est trop bas de 5 mm : appuyez **5 fois sur ▲** (un millimètre
   par appui) ; trop haut : **▼** ; trop à droite : **◀** ; trop à gauche :
   **▶**. Il glisse un peu plus à chaque étiquette : corrigez l'**espace
   entre deux étiquettes**. Réimprimez le réglage jusqu'à ce qu'il tombe
   juste.

L'impression commence aussitôt, sans blanc avant la première étiquette, et
chaque série avance le papier d'un nombre entier d'étiquettes : une fois
réglée, chaque série retombe juste d'elle-même. La dernière étiquette
imprimée ressort au-delà de la barre de découpe, prête à décoller ; celle
qui la suit reste vierge. **N'appuyez pas sur le bouton d'avance du papier**
entre deux séries : sinon, réimprimez le réglage.

### Imprimer les étiquettes

**Produits** → choisissez le produit → **Imprimer les étiquettes**.

La fenêtre propose une étiquette par article en stock. Modifiez les nombres
si besoin, choisissez l'imprimante et la taille des étiquettes la première
fois, vérifiez l'aperçu, puis **Imprimer**. Ces choix sont retenus pour ce
poste.

Quand vous créez une déclinaison avec une quantité en stock, le logiciel
propose aussitôt d'imprimer ses étiquettes.

L'étiquette porte le nom du magasin, l'article, le prix en grand et le
code-barres. La taille et la couleur peuvent être ajoutées, le prix et le
nom du magasin retirés (cases à cocher de la fenêtre).

### Scanner

La douchette se branche en USB et fonctionne comme un clavier : rien à
installer.

- **Caisse** : scannez l'étiquette, l'article s'ajoute au panier. Scannez-le
  encore, la quantité augmente.
- **Produits** et **Stock** : scannez dans le champ de recherche pour
  retrouver l'article.

**L'étiquette sort décalée sur le côté, ou à cheval sur deux étiquettes ?**
Le pilote de l'imprimante imprime sur un autre papier que vos étiquettes de
40 × 20 mm : la fenêtre des étiquettes le signale alors en rouge, sous le
nom de l'imprimante, avec la taille qu'il utilise. Réglez le format
40 × 20 mm et le papier « étiquettes avec espace » dans les options
d'impression de Windows (étape 2), faites repérer les étiquettes à
l'imprimante (étape 3), puis rouvrez la fenêtre : la ligne rouge disparaît.

**La douchette ne lit pas l'étiquette ?** Les barres sont peut-être trop
pâles : augmentez la **densité** (ou *darkness*) dans les options
d'impression de l'imprimante.

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

### « La base de données n'a pas pu être préparée »

Ce message n'apparaît qu'au tout premier démarrage. Il indique lui-même les
causes possibles ; voici quoi faire, dans l'ordre.

**1. Ne lancez pas le logiciel en tant qu'administrateur.**

C'est la cause la plus fréquente. Fermez tout, puis rouvrez le logiciel par
un **simple double-clic** sur `GestionMagasin.exe`. N'utilisez pas
« Exécuter en tant qu'administrateur ».

Si vous avez créé un raccourci : clic droit dessus → **Propriétés** →
**Avancé** → décochez **Exécuter en tant qu'administrateur**.

**2. Déplacez le dossier du logiciel.**

Certains emplacements sont protégés par Windows. Placez le dossier dans un
endroit simple :

```
C:\GestionMagasin
```

Évitez `Program Files`, les dossiers synchronisés (OneDrive, Google Drive),
les clés USB et les lecteurs réseau.

**3. Ajoutez une exception dans l'antivirus.**

Sur le dossier complet du logiciel, `pgsql` compris.

---

### « Copie incomplète » — le dossier `pgsql` est absent

L'archive n'a pas été décompressée entièrement, ou l'antivirus a mis une
partie des fichiers en quarantaine.

1. Supprimez le dossier obtenu
2. Décompressez **de nouveau** l'archive reçue, en entier
3. Vérifiez que le dossier **`pgsql`** est bien à côté de
   `GestionMagasin.exe`

Ne copiez pas les fichiers un par un depuis l'archive : Windows en oublie.

---

### « Le serveur de base de données n'a pas pu démarrer »

Le logiciel choisit lui-même un port réseau libre à chaque démarrage, et
essaie soixante numéros : ce message ne devrait donc plus apparaître pour
cette raison. S'il indique que Windows refuse l'ouverture d'un port,
**redémarrez l'ordinateur** — Windows réattribue alors ses plages réservées,
et cela suffit presque toujours.



La fenêtre indique la cause à la ligne **« Cause : »**. Suivez-la.

Sinon, dans l'ordre :

1. Fermez complètement le logiciel
2. Ouvrez le **Gestionnaire des tâches** (Ctrl + Maj + Échap) et terminez
   tout processus nommé **postgres**
3. Relancez le logiciel par un **simple double-clic** — jamais par
   « Exécuter en tant qu'administrateur »

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

## Votre logo

Le logo du magasin est fourni avec le logiciel : c'est le fichier
**`logo.png`** à la racine du dossier. `publier.ps1` le livre à côté du
programme, et il devient :

- l'emblème affiché dans le menu et sur l'écran de connexion ;
- le logo imprimé en tête des tickets ;
- l'icône du programme — barre des tâches, raccourci du bureau, Explorateur ;
- l'icône de chaque fenêtre.

Pour en changer, remplacez ce fichier par un autre du même nom avant de
publier.

Sur le ticket, le logo est réduit à deux tons — l'imprimante ne sait que
brûler ou laisser blanc. Le partage entre les teintes sombres et claires
est calculé d'après le logo lui-même : le lion doré sort blanc sur le
disque noir. Un logo à aplats nets s'imprime mieux qu'une photographie.

Une image carrée donne le meilleur résultat. Sans logo, l'emblème dessiné
dans le logiciel prend le relais : le logiciel n'est jamais livré sans
enseigne.

Vous pouvez aussi changer le logo depuis **Paramètres** (F12) sans
republier : le menu et les barres de titre suivent immédiatement.

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

| Combinaison | Effet |
|---|---|
| **Ctrl + F5** | Recharger l'écran affiché |
| **Ctrl + F11** | Passer en plein écran, ou en sortir |
| **Échap** | Sortir du plein écran |

En caisse, le curseur reste dans le champ de scan : vous pouvez enchaîner
les articles à la douchette sans jamais toucher la souris.

**Plein écran.** Le bouton aux quatre flèches, en haut à droite à côté
d'**Actualiser**, fait disparaître le cadre de la fenêtre et la barre des
tâches : l'écran entier revient au logiciel, ce qui vaut deux rangées de
vignettes de plus en caisse. Le même bouton, ou la touche **Échap**,
rétablit la fenêtre.

**Écran tactile.** Toutes les listes se font glisser au doigt, avec
l'inertie habituelle. En caisse, les vignettes du rayon et les cases de
taille sont dimensionnées pour le doigt : une vente entière se passe de
clavier et de souris.

Trois façons d'ajouter un article, au choix :

| | Quand l'employer |
|---|---|
| **Scanner** le code-barres | L'étiquette est lisible — c'est le plus rapide |
| **Rechercher** par nom ou référence | L'article est connu de nom |
| **Cliquer** sur le produit dans « Tout le rayon » | L'étiquette est arrachée, ou le client hésite |

Le rayon se range par **famille** : des onglets en haut (Tout le rayon,
Chemises, Pantalons, Chaussures…) limitent l'affichage à un rayon.

La plupart des produits montrent leurs **couleurs et leurs tailles
directement sur la vignette** : une pastille pour la couleur, une case par
taille avec le stock restant. Un appui sur la case vend l'article — un seul
geste.

Les produits très déclinés — cinq couleurs et six tailles — ouvrent le
choix dans un panneau, faute de place sur la vignette.

Dans les deux cas, une taille épuisée reste affichée, **barrée** : vous
savez qu'elle existe et qu'elle manque.
