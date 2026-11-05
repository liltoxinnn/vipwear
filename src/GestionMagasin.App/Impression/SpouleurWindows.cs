using System.ComponentModel;
using System.Printing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace GestionMagasin.App.Impression;

/// <summary>Échec d'impression d'un ticket, décrit en français pour le magasin.</summary>
public sealed class ImpressionTicketException(string message, Exception? cause = null)
    : Exception(message, cause);

/// <summary>
/// Envoi direct des commandes de l'imprimante de tickets au spouleur de
/// Windows.
///
/// Le travail est remis « brut » : Windows le transmet à l'imprimante sans
/// le faire passer par le moteur de mise en page du pilote. C'est ce qui
/// garantit la longueur exacte du ticket — un pilote qui croit imprimer sur
/// une page de 21 cm ferait avancer 21 cm de papier pour trois articles — et
/// c'est ce qui permet la coupe et l'ouverture du tiroir.
///
/// Il faut tout de même que l'imprimante soit installée dans Windows : c'est
/// le pilote du fabricant, ou à défaut le pilote « Generic / Text Only »,
/// qui crée la file d'attente à qui le travail est remis.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class SpouleurWindows
{
    private const int ErreurNomImprimanteInvalide = 1801;
    private const int ErreurTypeDonneesInvalide = 1804;

    /// <summary>
    /// Imprimantes installées sur le poste, par ordre alphabétique. Une liste
    /// vide si le spouleur de Windows est arrêté : l'écran des réglages le
    /// dit alors, au lieu de tomber en erreur.
    /// </summary>
    public static IReadOnlyList<string> ListerImprimantes()
    {
        try
        {
            using var serveur = new LocalPrintServer();

            var files = serveur.GetPrintQueues(
                [EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections]);

            var noms = new List<string>();

            foreach (var file in files)
            {
                using (file)
                {
                    noms.Add(file.FullName);
                }
            }

            noms.Sort(StringComparer.CurrentCultureIgnoreCase);

            return noms;
        }
        catch (Exception erreur) when (erreur is PrintSystemException or Win32Exception
                                           or InvalidOperationException)
        {
            return [];
        }
    }

    /// <summary>Remet un travail d'impression brut à l'imprimante désignée.</summary>
    /// <param name="nomImprimante">Nom de l'imprimante tel que Windows le connaît.</param>
    /// <param name="nomDocument">Nom du travail affiché dans la file d'attente de Windows.</param>
    /// <param name="donnees">Commandes ESC/POS du ticket.</param>
    public static void EnvoyerBrut(string nomImprimante, string nomDocument, byte[] donnees)
    {
        if (!OpenPrinter(nomImprimante, out var imprimante, IntPtr.Zero))
        {
            throw Echec(nomImprimante, Marshal.GetLastPInvokeError());
        }

        try
        {
            var document = new InformationsDocument { NomDocument = nomDocument };

            if (StartDocPrinter(imprimante, 1, document) == 0)
            {
                throw Echec(nomImprimante, Marshal.GetLastPInvokeError());
            }

            try
            {
                if (!StartPagePrinter(imprimante))
                {
                    throw Echec(nomImprimante, Marshal.GetLastPInvokeError());
                }

                try
                {
                    if (!WritePrinter(imprimante, donnees, donnees.Length, out var ecrits))
                    {
                        throw Echec(nomImprimante, Marshal.GetLastPInvokeError());
                    }

                    if (ecrits != donnees.Length)
                    {
                        throw new ImpressionTicketException(
                            $"Le ticket n'est parvenu qu'en partie à « {nomImprimante} » " +
                            $"({ecrits} octets sur {donnees.Length}). Vérifiez le câble et réimprimez-le.");
                    }
                }
                finally
                {
                    EndPagePrinter(imprimante);
                }
            }
            finally
            {
                EndDocPrinter(imprimante);
            }
        }
        finally
        {
            ClosePrinter(imprimante);
        }
    }

    private static ImpressionTicketException Echec(string nomImprimante, int code) => code switch
    {
        ErreurNomImprimanteInvalide => new ImpressionTicketException(
            $"L'imprimante « {nomImprimante} » est introuvable. Elle a peut-être été renommée ou " +
            "retirée de Windows : choisissez-la de nouveau dans Paramètres, onglet Imprimante de tickets."),

        ErreurTypeDonneesInvalide => new ImpressionTicketException(
            $"L'imprimante « {nomImprimante} » refuse l'envoi direct des tickets : son pilote est d'un " +
            "type récent qui ne l'accepte pas. Installez-la avec le pilote fourni par le fabricant, ou " +
            "avec le pilote Windows « Generic / Text Only »."),

        _ => new ImpressionTicketException(
            $"Windows n'a pas pu envoyer le ticket à « {nomImprimante} » : " +
            $"{new Win32Exception(code).Message} (code {code}).",
            new Win32Exception(code))
    };

    // ------------------------------------------------------------------
    // winspool.drv
    // ------------------------------------------------------------------

    /// <summary>DOC_INFO_1W : nom du travail, fichier de sortie, type de données.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class InformationsDocument
    {
        public string NomDocument = "Ticket";

        public string? FichierSortie;

        /// <summary>« RAW » : les octets vont à l'imprimante sans retouche.</summary>
        public string TypeDonnees = "RAW";
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string nom, out IntPtr imprimante, IntPtr parametres);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr imprimante);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int StartDocPrinter(IntPtr imprimante, int niveau, [In] InformationsDocument document);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr imprimante);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr imprimante);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr imprimante);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr imprimante, byte[] donnees, int nombre, out int ecrits);
}
