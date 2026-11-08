using GestionMagasin.Application.Common;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Domain.Entities;

namespace GestionMagasin.Application.Impression;

/// <summary>Corps d'une ligne de ticket.</summary>
public enum StyleLigne
{
    Normal,

    Gras,

    /// <summary>
    /// Corps une fois et demie plus grand, en gras : le nom du magasin et le
    /// total. Une ligne de ce corps contient deux tiers des caractères d'une
    /// ligne normale.
    /// </summary>
    Grand,

    /// <summary>
    /// QR code, centré. Le texte de la ligne est le contenu du code, et non
    /// du texte à imprimer : c'est le dessin qui le transforme en carrés.
    /// </summary>
    CodeQr
}

/// <summary>
/// Une ligne du ticket, déjà mise à sa largeur : le texte est complété
/// d'espaces, centré ou aligné, et n'a plus qu'à être dessiné à gauche.
/// </summary>
public sealed record LigneTicket(string Texte, StyleLigne Style = StyleLigne.Normal);

/// <summary>
/// Composition du ticket de caisse en colonnes.
///
/// Le ticket est écrit en caractères à chasse fixe, comme sur toutes les
/// imprimantes de caisse : chaque montant tombe sous le précédent, et rien ne
/// dépend de la police qu'aura le poste. Cette classe ne sait rien de
/// l'imprimante ni de Windows — elle produit des lignes de texte, ce qui
/// permet d'en vérifier chaque règle sans matériel : aucune ligne ne déborde,
/// les montants sont alignés, la monnaie rendue est juste.
/// </summary>
public static class CompositionTicket
{
    /// <summary>Points de suspension ajoutés à un texte coupé.</summary>
    private const char Coupure = '…';

    /// <summary>Lignes au plus consacrées à la désignation d'un article.</summary>
    private const int LignesParDesignation = 2;

    /// <summary>
    /// Compose le ticket d'une vente.
    /// </summary>
    /// <param name="vente">Vente enregistrée.</param>
    /// <param name="magasin">Identité et mentions du magasin.</param>
    /// <param name="reglages">Réglages du poste, dont la largeur du rouleau.</param>
    /// <param name="montantRecu">
    /// Somme remise par le client, connue seulement au moment de la vente.
    /// Lors d'une réimpression elle est perdue : le ticket n'indique alors ni
    /// la somme reçue ni la monnaie rendue, plutôt que d'en inventer.
    /// </param>
    /// <param name="mention">Bandeau imprimé sous l'en-tête, pour un ticket d'essai.</param>
    public static IReadOnlyList<LigneTicket> Composer(
        VenteDto vente,
        ParametresMagasin magasin,
        ReglagesTicket reglages,
        decimal? montantRecu = null,
        string? mention = null)
    {
        var colonnes = reglages.Colonnes;
        var grandes = ColonnesGrandes(colonnes);
        var devise = magasin.SymboleDevise;

        var lignes = new List<LigneTicket>();

        void Ajouter(string texte, StyleLigne style = StyleLigne.Normal) =>
            lignes.Add(new LigneTicket(texte, style));

        void AjouterCentre(string? texte, StyleLigne style = StyleLigne.Normal)
        {
            if (string.IsNullOrWhiteSpace(texte))
            {
                return;
            }

            var largeur = style == StyleLigne.Grand ? grandes : colonnes;

            foreach (var morceau in RepartirCentre(texte, largeur))
            {
                Ajouter(Centrer(morceau, largeur), style);
            }
        }

        void AjouterMontant(string libelle, string montant, StyleLigne style = StyleLigne.Normal)
        {
            var largeur = style == StyleLigne.Grand ? grandes : colonnes;

            foreach (var morceau in DeuxColonnes(libelle, montant, largeur))
            {
                Ajouter(morceau, style);
            }
        }

        // ---------------- En-tête ----------------

        // Le registre du commerce et le NIF ne figurent que sur la facture :
        // le magasin a choisi de ne pas les imprimer sur les tickets.
        AjouterCentre(magasin.NomMagasin, StyleLigne.Grand);
        AjouterCentre(magasin.Adresse);
        AjouterCentre(Renseigne("Tél. : ", magasin.Telephone));

        if (!string.IsNullOrWhiteSpace(mention))
        {
            Ajouter(string.Empty);
            AjouterCentre(mention, StyleLigne.Grand);
        }

        Ajouter(Trait('=', colonnes));

        // ---------------- Vente ----------------

        // Le compte qui a encaissé n'est pas imprimé : il reste dans la vente,
        // consultable à l'écran Ventes, mais ne regarde pas le client.
        Ajouter(Tronquer($"Ticket n° {vente.NumeroVente}", colonnes), StyleLigne.Gras);
        Ajouter(Tronquer($"Date : {FormatageMontant.FormaterDateHeure(vente.DateVente)}", colonnes));

        if (!string.IsNullOrWhiteSpace(vente.ClientNom))
        {
            Ajouter(Tronquer($"Client : {vente.ClientNom}", colonnes));
        }

        Ajouter(Trait('-', colonnes));

        // ---------------- Articles ----------------

        foreach (var ligne in vente.Lignes)
        {
            var designation = Envelopper(ligne.Designation, colonnes);

            for (var i = 0; i < Math.Min(designation.Count, LignesParDesignation); i++)
            {
                // La dernière ligne autorisée porte la coupure si le nom
                // continuait : on voit qu'il manque quelque chose.
                var texte = i == LignesParDesignation - 1 && designation.Count > LignesParDesignation
                    ? Tronquer(designation[i] + " " + designation[i + 1], colonnes)
                    : designation[i];

                Ajouter(texte, StyleLigne.Gras);
            }

            AjouterMontant(
                $"  {ligne.Quantite} × {FormatageMontant.FormaterSansDevise(ligne.PrixUnitaire)}",
                FormatageMontant.Formater(ligne.Total, devise));

            if (ligne.Remise > 0)
            {
                AjouterMontant("  Remise", "-" + FormatageMontant.Formater(ligne.Remise, devise));
            }
        }

        Ajouter(Trait('-', colonnes));

        // ---------------- Totaux ----------------

        AjouterMontant("Sous-total", FormatageMontant.Formater(vente.SousTotal, devise));

        if (vente.Remise > 0)
        {
            AjouterMontant("Remise", "-" + FormatageMontant.Formater(vente.Remise, devise));
        }

        AjouterMontant("TOTAL", FormatageMontant.Formater(vente.Total, devise), StyleLigne.Grand);

        foreach (var paiement in vente.Paiements)
        {
            AjouterMontant(paiement.LibelleModePaiement, FormatageMontant.Formater(paiement.Montant, devise));
        }

        // Reçu et rendu ne s'impriment que s'ils apportent quelque chose : un
        // client qui a donné l'appoint n'a pas de monnaie à vérifier.
        if (montantRecu is { } recu && recu > vente.Total)
        {
            AjouterMontant("Reçu", FormatageMontant.Formater(recu, devise));
            AjouterMontant("Rendu", FormatageMontant.Formater(recu - vente.Total, devise), StyleLigne.Gras);
        }

        if (vente.MontantRestant > 0)
        {
            AjouterMontant("Reste à payer", FormatageMontant.Formater(vente.MontantRestant, devise),
                StyleLigne.Gras);
        }

        Ajouter(string.Empty);
        AjouterCentre($"Nombre d'articles : {vente.NombreArticles}");
        Ajouter(Trait('=', colonnes));

        // ---------------- Pied ----------------

        // Le message peut tenir sur plusieurs lignes saisies à la main :
        // chacune est respectée, puis repliée si elle est trop longue.
        AjouterCentre(magasin.InformationsTicket);

        // Les conditions d'échange ne sont pas imprimées : le magasin a choisi
        // de les retirer du ticket. Le délai reste réglé dans les paramètres,
        // où il gouverne les retours acceptés.

        // Le QR code ferme le ticket : c'est la dernière chose qu'on regarde
        // avant de le ranger, et il ne gêne la lecture de rien.
        if (!string.IsNullOrWhiteSpace(magasin.LienQrCodeTicket))
        {
            // Pas de ligne blanche avant : la marge que le code exige autour
            // de lui en tient lieu.
            Ajouter(magasin.LienQrCodeTicket.Trim(), StyleLigne.CodeQr);
            AjouterCentre(LegendeQrCode);
        }

        return lignes;
    }

    /// <summary>
    /// Ligne imprimée sous le QR code. Elle dit ce qu'on gagne à le scanner,
    /// sans nommer de réseau : le même texte vaut pour un lien Instagram,
    /// Facebook ou Linktree. Elle tient sur une ligne de 58 mm.
    /// </summary>
    public const string LegendeQrCode = "Suivez-nous sur nos réseaux";

    /// <summary>Caractères d'une ligne en grand corps, pour une largeur normale donnée.</summary>
    public static int ColonnesGrandes(int colonnes) => colonnes * 2 / 3;

    // ==================================================================
    // Mise en page en chasse fixe
    // ==================================================================

    /// <summary>
    /// Découpe un texte destiné à être centré : adresse, message de pied.
    ///
    /// Les retours à la ligne saisis sont respectés — le champ Adresse en
    /// accepte, et un retour laissé dans le texte aurait été dessiné au
    /// milieu d'une ligne, décalant tout le reste. Une ligne trop longue
    /// est coupée de préférence après une virgule, là où l'auteur a marqué
    /// la pause : « Ighil Ouazoug, » puis « à 50 mètres à côté de la
    /// mosquée », plutôt que « Ighil Ouazoug, à 50 mètres à » puis « côté
    /// de la mosquée ».
    /// </summary>
    public static IReadOnlyList<string> RepartirCentre(string? texte, int largeur)
    {
        var lignes = new List<string>();

        if (string.IsNullOrWhiteSpace(texte) || largeur <= 0)
        {
            return lignes;
        }

        foreach (var brut in texte.Replace("\r", string.Empty).Split('\n'))
        {
            var paragraphe = brut.Trim();

            if (paragraphe.Length == 0)
            {
                continue;
            }

            if (paragraphe.Length <= largeur)
            {
                lignes.Add(paragraphe);
                continue;
            }

            var virgule = paragraphe.LastIndexOf(", ", StringComparison.Ordinal);

            while (virgule > 0)
            {
                var debut = paragraphe[..(virgule + 1)];
                var fin = paragraphe[(virgule + 2)..];

                if (debut.Length <= largeur && fin.Length <= largeur)
                {
                    lignes.Add(debut);
                    lignes.Add(fin);
                    break;
                }

                virgule = paragraphe.LastIndexOf(", ", virgule - 1, StringComparison.Ordinal);
            }

            if (virgule <= 0)
            {
                lignes.AddRange(Equilibrer(paragraphe, largeur));
            }
        }

        return lignes;
    }

    /// <summary>
    /// Replie un texte centré. S'il tient en deux lignes, la coupure est
    /// choisie pour que les deux soient de longueur voisine : « Merci de
    /// votre visite » puis « et à bientôt ! », plutôt qu'une première ligne
    /// pleine et « bientôt ! » orphelin dessous.
    /// </summary>
    private static IReadOnlyList<string> Equilibrer(string paragraphe, int largeur)
    {
        var gourmand = Envelopper(paragraphe, largeur);

        if (gourmand.Count != 2)
        {
            return gourmand;
        }

        var mots = paragraphe.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        IReadOnlyList<string> meilleur = gourmand;
        var plusLongue = Math.Max(gourmand[0].Length, gourmand[1].Length);

        for (var coupure = 1; coupure < mots.Length; coupure++)
        {
            var debut = string.Join(' ', mots[..coupure]);
            var fin = string.Join(' ', mots[coupure..]);
            var longueur = Math.Max(debut.Length, fin.Length);

            // À longueur égale, la ligne du haut la plus longue l'emporte :
            // un texte centré se lit mieux en pointe vers le bas.
            var meilleure = longueur < plusLongue
                            || (longueur == plusLongue && debut.Length > meilleur[0].Length);

            if (debut.Length <= largeur && fin.Length <= largeur && meilleure)
            {
                meilleur = [debut, fin];
                plusLongue = longueur;
            }
        }

        return meilleur;
    }

    /// <summary>
    /// Replie un texte sur plusieurs lignes, entre les mots. Un mot plus long
    /// que la ligne — une référence, un courriel — est coupé net : mieux vaut
    /// un mot sur deux lignes qu'une ligne qui sort du papier.
    /// </summary>
    public static IReadOnlyList<string> Envelopper(string? texte, int largeur)
    {
        var lignes = new List<string>();

        if (string.IsNullOrWhiteSpace(texte) || largeur <= 0)
        {
            return lignes;
        }

        // Un tiret isolé reste avec ce qui le suit : « PANTALON CHINO — » en
        // fin de ligne puis « Beige / 42 » seul ne se lit pas comme un nom et
        // sa couleur. L'espace qui suit le tiret devient insécable le temps
        // du repli, puis redevient une espace ordinaire.
        const char Insecable = '\u00A0';

        // Un retour à la ligne égaré ne doit jamais atteindre le dessin : il
        // y casserait la ligne et décalerait tout ce qui suit.
        texte = texte.Replace("\r", " ").Replace('\n', ' ');

        texte = texte.Replace(" — ", " —" + Insecable).Replace(" - ", " -" + Insecable);

        var courante = string.Empty;

        foreach (var brut in texte.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var mot = brut;

            while (mot.Length > largeur)
            {
                if (courante.Length > 0)
                {
                    lignes.Add(courante);
                    courante = string.Empty;
                }

                lignes.Add(mot[..largeur]);
                mot = mot[largeur..];
            }

            if (courante.Length == 0)
            {
                courante = mot;
            }
            else if (courante.Length + 1 + mot.Length <= largeur)
            {
                courante += " " + mot;
            }
            else
            {
                lignes.Add(courante);
                courante = mot;
            }
        }

        if (courante.Length > 0)
        {
            lignes.Add(courante);
        }

        for (var i = 0; i < lignes.Count; i++)
        {
            lignes[i] = lignes[i].Replace(Insecable, ' ');
        }

        return lignes;
    }

    /// <summary>Centre un texte sur la largeur, en le coupant s'il la dépasse.</summary>
    public static string Centrer(string texte, int largeur)
    {
        var coupe = Tronquer(texte.Trim(), largeur);
        var gauche = (largeur - coupe.Length) / 2;

        return new string(' ', gauche) + coupe;
    }

    /// <summary>
    /// Pose un libellé à gauche et un montant à droite.
    ///
    /// Le montant ne se coupe jamais : c'est lui que le client vérifie. Si les
    /// deux ne tiennent pas ensemble, le libellé garde sa ligne et le montant
    /// passe à la suivante, calé à droite.
    /// </summary>
    public static IReadOnlyList<string> DeuxColonnes(string libelle, string montant, int largeur)
    {
        if (libelle.Length + 1 + montant.Length <= largeur)
        {
            return [libelle + new string(' ', largeur - libelle.Length - montant.Length) + montant];
        }

        return
        [
            Tronquer(libelle, largeur),
            montant.Length >= largeur ? montant : new string(' ', largeur - montant.Length) + montant
        ];
    }

    /// <summary>Coupe un texte trop long et le signale par des points de suspension.</summary>
    public static string Tronquer(string texte, int largeur)
    {
        if (texte.Length <= largeur)
        {
            return texte;
        }

        return largeur <= 1 ? texte[..largeur] : texte[..(largeur - 1)] + Coupure;
    }

    private static string Trait(char motif, int largeur) => new(motif, largeur);

    private static string? Renseigne(string prefixe, string? valeur) =>
        string.IsNullOrWhiteSpace(valeur) ? null : prefixe + valeur.Trim();
}
