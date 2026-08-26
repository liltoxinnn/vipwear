using System.Net;
using System.Net.Sockets;

namespace GestionMagasin.ServeurEmbarque;

/// <summary>
/// Choisit le port d'écoute du serveur de base de données.
///
/// Le port ne peut pas être fixé une fois pour toutes. Windows réserve des
/// plages entières pour Hyper-V, WSL et Docker, et les réattribue à chaque
/// démarrage de la machine : un port libre hier peut être interdit demain,
/// sans que rien n'ait été installé. PostgreSQL échoue alors sur
/// « could not bind IPv4 address : Permission denied », et le logiciel ne
/// démarre plus du tout — sur un poste où il fonctionnait la veille.
///
/// Le port n'a besoin d'être ni stable ni prévisible : le serveur n'écoute
/// que sur 127.0.0.1 et n'est joint que par l'application, qui apprend le
/// port au démarrage.
/// </summary>
public static class SelecteurPort
{
    /// <summary>Nombre de ports essayés à partir du port souhaité.</summary>
    public const int Etendue = 60;

    /// <summary>
    /// Premier port réellement utilisable à partir de <paramref name="souhaite"/>.
    /// </summary>
    /// <param name="souhaite">Port préféré, essayé en premier.</param>
    /// <param name="utilisable">Épreuve d'un port : vrai s'il peut être ouvert.</param>
    /// <param name="etendue">Nombre de ports à essayer.</param>
    /// <returns>Le port retenu, ou null si aucun ne convient.</returns>
    public static int? Premier(int souhaite, Func<int, bool> utilisable, int etendue = Etendue)
    {
        for (var port = souhaite; port < souhaite + etendue; port++)
        {
            // Au-delà, les numéros ne sont plus valides.
            if (port > 65535)
            {
                return null;
            }

            if (utilisable(port))
            {
                return port;
            }
        }

        return null;
    }

    /// <summary>
    /// Vrai lorsqu'un port peut effectivement être ouvert sur la boucle locale.
    ///
    /// L'épreuve consiste à l'ouvrir pour de bon : demander au système la
    /// liste des ports occupés ne dirait rien des plages qu'il s'est
    /// réservées, et c'est précisément celles-là qui font échouer le serveur.
    /// </summary>
    public static bool Utilisable(int port)
    {
        TcpListener? ecouteur = null;

        try
        {
            ecouteur = new TcpListener(IPAddress.Loopback, port);
            ecouteur.Start();

            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            ecouteur?.Stop();
        }
    }
}
