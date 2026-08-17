using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using GestionMagasin.Application.Common;

namespace GestionMagasin.App.Converters;

/// <summary>Affiche un montant au format « 12 500,00 DA ».</summary>
public class ConvertisseurMontant : IValueConverter
{
    /// <summary>Symbole de devise, renseigné au démarrage depuis les paramètres du magasin.</summary>
    public static string SymboleDevise { get; set; } = FormatageMontant.SymboleParDefaut;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            decimal montant => FormatageMontant.Formater(montant, SymboleDevise),
            double montant => FormatageMontant.Formater((decimal)montant, SymboleDevise),
            int montant => FormatageMontant.Formater(montant, SymboleDevise),
            null => string.Empty,
            _ => value.ToString() ?? string.Empty
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("La conversion inverse d'un montant n'est pas prévue.");
}

/// <summary>Affiche une date universelle en heure locale, au format français.</summary>
public class ConvertisseurDate : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime date)
        {
            return string.Empty;
        }

        var avecHeure = parameter as string == "heure";

        return avecHeure
            ? FormatageMontant.FormaterDateHeure(date)
            : FormatageMontant.FormaterDate(date);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("La conversion inverse d'une date n'est pas prévue.");
}

/// <summary>Rend un élément visible selon un booléen. « inverse » inverse la logique.</summary>
public class ConvertisseurBooleenVisibilite : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var valeur = value is bool booleen && booleen;

        if (parameter as string == "inverse")
        {
            valeur = !valeur;
        }

        return valeur ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility visibilite && visibilite == Visibility.Visible;
}

/// <summary>Inverse un booléen : utilisé pour désactiver un bouton pendant un traitement.</summary>
public class ConvertisseurBooleenInverse : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not bool booleen || !booleen;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not bool booleen || !booleen;
}

/// <summary>Rend un élément visible lorsque la valeur n'est ni nulle ni vide.</summary>
public class ConvertisseurNonVideVisibilite : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var rempli = value switch
        {
            null => false,
            string texte => !string.IsNullOrWhiteSpace(texte),
            int nombre => nombre > 0,
            System.Collections.ICollection collection => collection.Count > 0,
            _ => true
        };

        if (parameter as string == "inverse")
        {
            rempli = !rempli;
        }

        return rempli ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("La conversion inverse n'est pas prévue.");
}

/// <summary>
/// Choisit la couleur d'une pastille d'état à partir de son libellé.
/// Le paramètre « fond » retourne la version atténuée destinée à l'arrière-plan.
/// </summary>
public class ConvertisseurCouleurEtat : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var etat = value?.ToString() ?? string.Empty;
        var pourFond = parameter as string == "fond";

        var (texte, fond) = etat switch
        {
            "Disponible" or "Payée" or "Reçu" or "Validé" or "Actif" or "Revendable"
                => ("#1E7E34", "#E6F4EA"),

            "Stock faible" or "Partiellement payée" or "Partiellement reçu"
                or "En attente de réception" or "Partiellement retournée"
                => ("#B7791F", "#FDF3E1"),

            "Rupture" or "Annulée" or "Annulé" or "Refusé" or "Endommagé" or "Inactif" or "Inactive"
                => ("#C0392B", "#FBEAE8"),

            "Retournée" => ("#2C7BE5", "#E8F1FD"),

            _ => ("#5A6577", "#EEF1F6")
        };

        var couleur = (Color)ColorConverter.ConvertFromString(pourFond ? fond : texte);

        return new SolidColorBrush(couleur);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("La conversion inverse n'est pas prévue.");
}

/// <summary>Transforme un code hexadécimal de couleur en pinceau affichable.</summary>
public class ConvertisseurCodeCouleur : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var code = value as string;

        if (string.IsNullOrWhiteSpace(code))
        {
            return new SolidColorBrush(Colors.Transparent);
        }

        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(code));
        }
        catch (FormatException)
        {
            // Un code mal saisi ne doit pas faire échouer l'affichage.
            return new SolidColorBrush(Colors.Transparent);
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("La conversion inverse n'est pas prévue.");
}

/// <summary>
/// Convertit une valeur en largeur de barre pour les graphiques du tableau
/// de bord. Le paramètre porte la largeur maximale disponible en pixels.
/// </summary>
public class ConvertisseurProportion : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2
            || values[0] is not decimal valeur
            || values[1] is not decimal maximum
            || maximum <= 0)
        {
            return 0d;
        }

        var largeurMaximale = double.TryParse(
            parameter as string, NumberStyles.Any, CultureInfo.InvariantCulture, out var largeur)
            ? largeur
            : 200d;

        return (double)(valeur / maximum) * largeurMaximale;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("La conversion inverse n'est pas prévue.");
}

/// <summary>Compare une valeur à un paramètre : sert à cocher l'onglet de menu actif.</summary>
public class ConvertisseurEgalite : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString();

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("La conversion inverse n'est pas prévue.");
}
