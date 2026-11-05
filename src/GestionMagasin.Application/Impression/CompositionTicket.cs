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
    Grand
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

            foreach (var morceau in Envelopper(texte, largeur))
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

        if (!string.IsNullOrWhiteSpace(magasin.InformationsTicket))
        {
            // Le message peut tenir sur plusieurs lignes saisies à la main :
            // chacune est respectée, puis repliée si elle est trop longue.
            foreach (var paragraphe in magasin.InformationsTicket.Replace("\r", string.Empty).Split('\n'))
            {
                AjouterCentre(paragraphe);
            }
        }

        AjouterCentre(
            $"Échange ou retour sous {magasin.DelaiRetourJours} jours sur présentation du ticket.");

        return lignes;
    }

    /// <summary>Caractères d'une ligne en grand corps, pour une largeur normale donnée.</summary>
    public static int ColonnesGrandes(int colonnes) => colonnes * 2 / 3;

    // ==================================================================
    // Mise en page en chasse fixe
    // ==================================================================

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
