using System.Text.RegularExpressions;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Contrôles sur les fichiers d'interface.
///
/// Le compilateur ne vérifie pas le contenu des liaisons XAML : une ressource
/// absente ou du mauvais type ne se manifeste qu'à l'exécution, souvent chez
/// le client et sur un seul écran. Chacune des règles ci-dessous correspond à
/// une panne réellement survenue.
/// </summary>
public class TestsInterface
{
    private static string DossierInterface
    {
        get
        {
            var dossier = new DirectoryInfo(AppContext.BaseDirectory);

            while (dossier is not null)
            {
                var candidat = Path.Combine(dossier.FullName, "src", "GestionMagasin.App");

                if (Directory.Exists(candidat))
                {
                    return candidat;
                }

                dossier = dossier.Parent;
            }

            throw new DirectoryNotFoundException(
                "Le projet d'interface est introuvable depuis " + AppContext.BaseDirectory);
        }
    }

    private static IEnumerable<string> FichiersXaml() =>
        Directory.EnumerateFiles(DossierInterface, "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    /// <summary>
    /// Une couleur n'est pas un pinceau. Employer l'une pour l'autre lève
    /// « n'est pas une valeur valide pour la propriété Stroke » au moment
    /// précis où l'écran s'affiche.
    /// </summary>
    [Fact]
    public void Aucune_couleur_n_est_employee_la_ou_un_pinceau_est_attendu()
    {
        var fautes = new List<string>();

        foreach (var fichier in FichiersXaml())
        {
            var lignes = File.ReadAllLines(fichier);

            for (var i = 0; i < lignes.Length; i++)
            {
                foreach (Match correspondance in
                         Regex.Matches(lignes[i], @"(\w+)\s*=\s*""\{StaticResource\s+(Couleur\w+)\s*\}"""))
                {
                    // « Color="{StaticResource CouleurX}" » est le seul usage correct.
                    if (correspondance.Groups[1].Value != "Color")
                    {
                        fautes.Add($"{Path.GetFileName(fichier)}:{i + 1} — " +
                                   $"{correspondance.Groups[1].Value} reçoit {correspondance.Groups[2].Value}");
                    }
                }

                foreach (Match correspondance in
                         Regex.Matches(lignes[i], @"\bColor\s*=\s*""\{StaticResource\s+(Pinceau\w+)\s*\}"""))
                {
                    fautes.Add($"{Path.GetFileName(fichier)}:{i + 1} — " +
                               $"Color reçoit le pinceau {correspondance.Groups[1].Value}");
                }
            }
        }

        Assert.True(fautes.Count == 0,
            "Couleur et pinceau confondus :" + Environment.NewLine + string.Join(Environment.NewLine, fautes));
    }

    /// <summary>
    /// Une ressource nommée mais jamais définie fait échouer l'écran qui
    /// l'emploie, et lui seul.
    /// </summary>
    [Fact]
    public void Toutes_les_ressources_nommees_sont_definies()
    {
        var definies = new HashSet<string>();
        var employees = new Dictionary<string, string>();

        foreach (var fichier in FichiersXaml())
        {
            var contenu = File.ReadAllText(fichier);

            foreach (Match correspondance in Regex.Matches(contenu, @"x:Key=""([^""]+)"""))
            {
                definies.Add(correspondance.Groups[1].Value);
            }

            foreach (Match correspondance in
                     Regex.Matches(contenu, @"\{StaticResource\s+([A-Za-z0-9_]+)\s*\}"))
            {
                employees.TryAdd(correspondance.Groups[1].Value, Path.GetFileName(fichier));
            }
        }

        var absentes = employees.Where(e => !definies.Contains(e.Key)).ToList();

        Assert.True(absentes.Count == 0,
            "Ressources employées mais jamais définies :" + Environment.NewLine +
            string.Join(Environment.NewLine, absentes.Select(a => $"  {a.Key} (vue dans {a.Value})")));
    }

    /// <summary>
    /// Un dictionnaire fusionné ne voit que ceux fusionnés avant lui. Cette
    /// règle avait empêché l'ouverture de cinq écrans, dont la caisse.
    /// </summary>
    [Fact]
    public void Chaque_dictionnaire_ne_depend_que_de_ceux_fusionnes_avant_lui()
    {
        var dossier = Path.Combine(DossierInterface, "Resources");

        var ordre = Regex.Matches(
                File.ReadAllText(Path.Combine(DossierInterface, "App.xaml")),
                @"<ResourceDictionary\s+Source=""Resources/([^""]+)""\s*/>")
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.NotEmpty(ordre);

        var visibles = new HashSet<string>();
        var fautes = new List<string>();

        foreach (var nom in ordre)
        {
            var contenu = File.ReadAllText(Path.Combine(dossier, nom));

            foreach (Match correspondance in Regex.Matches(contenu, @"x:Key=""([^""]+)"""))
            {
                visibles.Add(correspondance.Groups[1].Value);
            }

            foreach (Match correspondance in
                     Regex.Matches(contenu, @"\{StaticResource\s+([A-Za-z0-9_]+)\s*\}"))
            {
                if (!visibles.Contains(correspondance.Groups[1].Value))
                {
                    fautes.Add($"{nom} emploie « {correspondance.Groups[1].Value} », " +
                               "défini plus tard ou ailleurs");
                }
            }
        }

        Assert.True(fautes.Count == 0,
            "Ordre de fusion non respecté :" + Environment.NewLine + string.Join(Environment.NewLine, fautes));
    }

    /// <summary>
    /// Une liaison écrite sur le même élément qu'un « DataContext » est
    /// résolue dans le NOUVEAU contexte, pas dans celui du parent.
    ///
    /// Écrire à la fois DataContext="{Binding ArticleChoisi}" et
    /// Visibility="{Binding ArticleChoisi, ...}" cherche donc
    /// « ArticleChoisi » sur l'article lui-même. La liaison échoue sans
    /// bruit — ni exception, ni trace — et la propriété garde sa valeur par
    /// défaut. Le panneau de choix de la caisse restait ainsi affiché en
    /// permanence par-dessus le rayon.
    ///
    /// Le remède tient en un conteneur : la visibilité dehors, le contexte
    /// dedans.
    /// </summary>
    [Fact]
    public void Aucun_element_ne_se_lie_a_la_propriete_dont_il_change_le_contexte()
    {
        var fautes = new List<string>();

        foreach (var fichier in FichiersXaml())
        {
            var contenu = File.ReadAllText(fichier);

            // Un élément va de « < » au « > » qui le referme.
            foreach (Match element in Regex.Matches(contenu, @"<[A-Za-z][^<>]*?/?>", RegexOptions.Singleline))
            {
                var contexte = Regex.Match(
                    element.Value, @"DataContext\s*=\s*""\{Binding\s+([A-Za-z_][A-Za-z0-9_]*)");

                if (!contexte.Success)
                {
                    continue;
                }

                var propriete = contexte.Groups[1].Value;

                foreach (Match liaison in Regex.Matches(
                             element.Value, @"(\w+)\s*=\s*""\{Binding\s+([A-Za-z_][A-Za-z0-9_.]*)"))
                {
                    if (liaison.Groups[1].Value == "DataContext")
                    {
                        continue;
                    }

                    var chemin = liaison.Groups[2].Value;

                    if (chemin == propriete || chemin.StartsWith(propriete + ".", StringComparison.Ordinal))
                    {
                        var ligne = contenu.Take(element.Index).Count(c => c == '\n') + 1;

                        fautes.Add($"{Path.GetFileName(fichier)}:{ligne} — " +
                                   $"{liaison.Groups[1].Value} se lie à « {chemin} » sur l'élément " +
                                   $"qui prend « {propriete} » pour contexte");
                    }
                }
            }
        }

        Assert.True(fautes.Count == 0,
            "Liaisons résolues dans le mauvais contexte :" + Environment.NewLine +
            string.Join(Environment.NewLine, fautes));
    }

    /// <summary>
    /// Le magasin encaisse sur écran tactile : les listes doivent se faire
    /// glisser au doigt.
    ///
    /// Le style qui l'autorise doit rester IMPLICITE, c'est-à-dire sans clé.
    /// Lui en donner une le rendrait inoffensif : il n'atteindrait plus les
    /// zones défilantes que WPF fabrique à l'intérieur des tableaux et des
    /// listes déroulantes, qui sont précisément celles que personne ne peut
    /// régler de l'extérieur. Rien n'échouerait — le défilement au doigt
    /// cesserait simplement de fonctionner, et seul un essai sur la borne le
    /// révélerait.
    /// </summary>
    [Fact]
    public void Le_defilement_au_doigt_reste_actif_partout()
    {
        var styles = File.ReadAllText(Path.Combine(DossierInterface, "Resources", "Styles.xaml"));

        var implicite = Regex.Match(
            styles,
            @"<Style\s+TargetType=""ScrollViewer""\s*>(.*?)</Style>",
            RegexOptions.Singleline);

        Assert.True(implicite.Success,
            "Le style implicite des zones défilantes est absent ou porte une clé : " +
            "le défilement au doigt ne s'appliquerait plus aux tableaux.");

        Assert.Contains("PanningMode", implicite.Groups[1].Value, StringComparison.Ordinal);

        // Aucun écran ne doit désactiver le défilement au doigt.
        var refus = new List<string>();

        foreach (var fichier in FichiersXaml())
        {
            var lignes = File.ReadAllLines(fichier);

            for (var i = 0; i < lignes.Length; i++)
            {
                if (Regex.IsMatch(lignes[i], @"PanningMode\s*=\s*""None"""))
                {
                    refus.Add($"{Path.GetFileName(fichier)}:{i + 1}");
                }
            }
        }

        Assert.True(refus.Count == 0,
            "Défilement au doigt désactivé :" + Environment.NewLine + string.Join(Environment.NewLine, refus));
    }

    /// <summary>
    /// Une zone défilante dont la barre apparaît « au besoin » change de
    /// largeur selon son contenu. Si ce contenu se répartit d'après la largeur
    /// — c'est le cas d'une grille de vignettes — chaque mesure en modifie une
    /// autre et la mise en page ne se stabilise jamais. Windows arrête alors le
    /// programme, pile d'appels saturée, sans erreur ni trace : c'est ce qui
    /// fermait la caisse à la validation d'une vente.
    /// </summary>
    [Fact]
    public void Aucune_grille_repartie_ne_defile_avec_une_barre_escamotable()
    {
        var fautes = new List<string>();

        foreach (var fichier in FichiersXaml())
        {
            var lignes = File.ReadAllLines(fichier);

            for (var i = 0; i < lignes.Length; i++)
            {
                if (!lignes[i].Contains("<ScrollViewer", StringComparison.Ordinal))
                {
                    continue;
                }

                // La balise ouvrante et le début de son contenu suffisent :
                // le modèle de disposition y est toujours déclaré.
                var portee = string.Join('\n', lignes.Skip(i).Take(12));

                var escamotable = portee.Contains(
                    "VerticalScrollBarVisibility=\"Auto\"", StringComparison.Ordinal);

                var repartie = portee.Contains("<WrapPanel", StringComparison.Ordinal)
                               || portee.Contains("ItemsPanel=", StringComparison.Ordinal);

                if (escamotable && repartie)
                {
                    fautes.Add($"{Path.GetFileName(fichier)}:{i + 1} — barre escamotable " +
                               "autour d'une grille répartie");
                }
            }
        }

        Assert.True(fautes.Count == 0,
            "Mise en page instable :" + Environment.NewLine + string.Join(Environment.NewLine, fautes));
    }

    /// <summary>Deux ressources de même nom : celle qui l'emporte est imprévisible.</summary>
    [Fact]
    public void Aucune_ressource_n_est_definie_deux_fois()
    {
        var comptes = new Dictionary<string, List<string>>();

        foreach (var fichier in FichiersXaml())
        {
            foreach (Match correspondance in
                     Regex.Matches(File.ReadAllText(fichier), @"x:Key=""([^""]+)"""))
            {
                var cle = correspondance.Groups[1].Value;

                if (!comptes.TryGetValue(cle, out var fichiers))
                {
                    comptes[cle] = fichiers = [];
                }

                fichiers.Add(Path.GetFileName(fichier));
            }
        }

        var doublons = comptes.Where(c => c.Value.Count > 1).ToList();

        Assert.True(doublons.Count == 0,
            "Ressources définies plusieurs fois :" + Environment.NewLine +
            string.Join(Environment.NewLine, doublons.Select(d => $"  {d.Key} : {string.Join(", ", d.Value)}")));
    }

    /// <summary>
    /// Une image réduite sans lissage haute qualité paraît sale.
    ///
    /// Le logo du magasin est fourni en grand — mille pixels de côté ou plus
    /// — puis affiché à cent, et à quarante-quatre dans le menu. La réduction
    /// rapide que WPF applique par défaut ne retient qu'un pixel sur dix : les
    /// contours se hachent et les lettres fines grésillent. Le magasin conclut
    /// que le logiciel a abîmé son logo.
    /// </summary>
    [Fact]
    public void Toute_image_affichee_est_lissee()
    {
        var fautes = new List<string>();

        foreach (var fichier in FichiersXaml())
        {
            var contenu = File.ReadAllText(fichier);

            var porteuses = Regex.Matches(contenu, @"<(?:ImageBrush|Image)\b").Count;

            if (porteuses == 0)
            {
                continue;
            }

            var lissages = Regex.Matches(
                contenu, @"RenderOptions\.BitmapScalingMode=""HighQuality""").Count;

            if (lissages < porteuses)
            {
                fautes.Add($"  {Path.GetFileName(fichier)} : {porteuses} image(s), {lissages} lissage(s)");
            }
        }

        Assert.True(fautes.Count == 0,
            "Images affichées sans lissage haute qualité :" + Environment.NewLine +
            string.Join(Environment.NewLine, fautes));
    }

    /// <summary>
    /// Une commande citée par le XAML doit exister.
    ///
    /// Le compilateur ne lit pas les liaisons : un nom mal orthographié
    /// produit un bouton qui ne fait rien, sans message ni trace. Le vendeur
    /// appuie, rien ne se passe, et il croit le logiciel bloqué.
    /// </summary>
    [Fact]
    public void Toute_commande_citee_par_l_interface_existe()
    {
        var declarees = new HashSet<string>(StringComparer.Ordinal);

        var sources = Directory.EnumerateFiles(DossierInterface, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

        foreach (var fichier in sources)
        {
            var contenu = File.ReadAllText(fichier);

            // Commandes engendrées par [RelayCommand] : le générateur nomme
            // « XxxCommand » la méthode « Xxx », et retire le suffixe Async.
            foreach (Match correspondance in
                     Regex.Matches(contenu, @"\[RelayCommand[^\]]*\]([^(){};]*)\("))
            {
                var mots = Regex.Matches(correspondance.Groups[1].Value, @"\w+");

                if (mots.Count == 0)
                {
                    continue;
                }

                var nom = mots[^1].Value;

                if (nom.EndsWith("Async", StringComparison.Ordinal))
                {
                    nom = nom[..^"Async".Length];
                }

                declarees.Add(nom + "Command");
            }

            // Commandes écrites à la main.
            foreach (Match correspondance in Regex.Matches(
                         contenu,
                         @"public\s+(?:I?RelayCommand|ICommand|IAsyncRelayCommand)[\w<>,\s?]*\s+(\w+)\s*(?:\{|=>)"))
            {
                declarees.Add(correspondance.Groups[1].Value);
            }
        }

        var citees = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var fichier in FichiersXaml())
        {
            foreach (Match correspondance in Regex.Matches(
                         File.ReadAllText(fichier), @"\{Binding\s+(?:[\w\.]*\.)?(\w+Command)\b"))
            {
                citees.TryAdd(correspondance.Groups[1].Value, Path.GetFileName(fichier));
            }
        }

        var absentes = citees.Where(c => !declarees.Contains(c.Key)).ToList();

        Assert.True(absentes.Count == 0,
            "Commandes citées par l'interface mais introuvables :" + Environment.NewLine +
            string.Join(Environment.NewLine, absentes.Select(a => $"  {a.Key} (vue dans {a.Value})")));
    }

    /// <summary>
    /// Une fenêtre qui n'arrondit pas sa mise en page rend du texte flou.
    ///
    /// WPF place les éléments sur des coordonnées fractionnaires. Une colonne
    /// proportionnelle — « 1,4 fois la largeur restante » — commence donc
    /// rarement sur un pixel entier, et tout ce qu'elle contient hérite de ce
    /// demi-pixel : les lettres sont dessinées à cheval sur deux pixels et
    /// paraissent baveuses. La colonne voisine, qui commence à zéro, reste
    /// nette. C'est ce qui rendait la fiche produit floue à côté de la liste,
    /// dans la même fenêtre et avec la même police.
    ///
    /// Trois réglages y remédient, et se posent sur la fenêtre puisqu'ils
    /// descendent à tout ce qu'elle contient.
    /// </summary>
    [Fact]
    public void Chaque_fenetre_arrondit_sa_mise_en_page_et_fixe_le_rendu_du_texte()
    {
        var manquants = new List<string>();

        foreach (var fichier in FichiersXaml())
        {
            var contenu = File.ReadAllText(fichier);

            // Seules les fenêtres portent ces réglages : une vue posée dans
            // une fenêtre en hérite.
            var ouverture = Regex.Match(contenu, @"<Window\b.*?>", RegexOptions.Singleline);

            if (!ouverture.Success)
            {
                continue;
            }

            var balise = ouverture.Value;
            var nom = Path.GetFileName(fichier);

            if (!balise.Contains("UseLayoutRounding=\"True\"", StringComparison.Ordinal))
            {
                manquants.Add($"  {nom} : UseLayoutRounding");
            }

            if (!balise.Contains("TextFormattingMode=\"Display\"", StringComparison.Ordinal))
            {
                manquants.Add($"  {nom} : TextOptions.TextFormattingMode");
            }

            if (!balise.Contains("TextRenderingMode=\"ClearType\"", StringComparison.Ordinal))
            {
                manquants.Add($"  {nom} : TextOptions.TextRenderingMode");
            }
        }

        Assert.True(manquants.Count == 0,
            "Fenêtres au rendu de texte non fixé :" + Environment.NewLine +
            string.Join(Environment.NewLine, manquants));
    }

    /// <summary>
    /// Un effet — une ombre portée, par exemple — fait dessiner tout le
    /// contenu de l'élément à travers une surface intermédiaire, où Windows
    /// renonce au lissage sous-pixel. Le texte y perd sa netteté et s'amincit.
    ///
    /// Une ombre de sept pour cent d'opacité ne vaut pas la lisibilité des
    /// chiffres qu'elle entoure, dans un logiciel qu'on lit toute la journée.
    /// </summary>
    [Fact]
    public void Aucun_panneau_ne_porte_d_effet_sur_son_contenu()
    {
        var fautes = new List<string>();

        foreach (var fichier in FichiersXaml())
        {
            var lignes = File.ReadAllLines(fichier);

            for (var i = 0; i < lignes.Length; i++)
            {
                if (Regex.IsMatch(lignes[i], @"<(DropShadowEffect|BlurEffect)\b"))
                {
                    fautes.Add($"  {Path.GetFileName(fichier)}:{i + 1}");
                }
            }
        }

        Assert.True(fautes.Count == 0,
            "Effets appliqués à du contenu textuel :" + Environment.NewLine +
            string.Join(Environment.NewLine, fautes));
    }
}
