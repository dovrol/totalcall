using TotalCall.Client.Domain.Predictions;
using TotalCall.Client.Infrastructure.Browser;

namespace TotalCall.Client.Application.Localization;

public sealed class LanguageService(BrowserLocalStorage localStorage)
{
    private static readonly IReadOnlyDictionary<AppLanguage, IReadOnlyDictionary<string, string>> Translations =
        new Dictionary<AppLanguage, IReadOnlyDictionary<string, string>>
        {
            [AppLanguage.Polish] = new Dictionary<string, string>
            {
                ["App.Tagline"] = "Fantasy trójbojowe",
                ["App.DataSource"] = "Statyczne dane JSON",
                ["App.Storage"] = "Zapis lokalny",
                ["Nav.Competitions"] = "Zawody",
                ["Nav.Sheffield"] = "Sheffield 2026",
                ["Nav.Worlds"] = "Worlds 2026",
                ["Nav.Menu"] = "Menu",
                ["Home.Eyebrow"] = "Typowania dla fanów trójboju",
                ["Home.Title"] = "TotalCall",
                ["Home.Description"] = "Wybierz zawody, przejdź przez moduły typowań i zapisz swoje predykcje lokalnie w przeglądarce.",
                ["Home.Loading"] = "Ładowanie zawodów...",
                ["Home.Empty"] = "Nie skonfigurowano jeszcze żadnych zawodów.",
                ["Home.CompetitionsLabel"] = "Aktywne konfiguracje",
                ["Home.CompetitionsHint"] = "Każde zawody mogą mieć inny zestaw pytań, ale korzystają z tych samych komponentów.",
                ["Common.Starts"] = "Start",
                ["Common.Locks"] = "Blokada typów",
                ["Common.Status"] = "Status",
                ["Common.OpenCompetition"] = "Otwórz zawody",
                ["Common.BackToCompetition"] = "Wróć do zawodów",
                ["Common.BackToCompetitions"] = "Wróć do zawodów",
                ["Common.Tbd"] = "Do ustalenia",
                ["Common.Required"] = "Wymagane",
                ["Common.Optional"] = "Opcjonalne",
                ["Common.NotAnswered"] = "Brak odpowiedzi",
                ["Common.Yes"] = "Tak",
                ["Common.No"] = "Nie",
                ["Common.Value"] = "Wartość",
                ["Common.Athlete"] = "Zawodnik",
                ["Common.SelectAthlete"] = "Wybierz zawodnika",
                ["Common.SelectOption"] = "Wybierz opcję",
                ["Common.NoValue"] = "brak wartości",
                ["Common.Gold"] = "Złoto",
                ["Common.Silver"] = "Srebro",
                ["Common.Bronze"] = "Brąz",
                ["Status.Upcoming"] = "Otwarte",
                ["Status.Locked"] = "Zablokowane",
                ["Status.Completed"] = "Zakończone",
                ["Status.Archived"] = "Archiwum",
                ["Competition.Loading"] = "Ładowanie zawodów...",
                ["Competition.NotFoundTitle"] = "Nie znaleziono zawodów",
                ["Competition.NotFoundDescription"] = "Brak konfiguracji JSON dla tych zawodów.",
                ["Competition.Section"] = "Informacje o zawodach",
                ["Competition.Federation"] = "Federacja",
                ["Competition.ConfigVersion"] = "Wersja konfiguracji",
                ["Competition.Data"] = "Dane",
                ["Competition.Athletes"] = "Zawodnicy",
                ["Competition.Categories"] = "Kategorie",
                ["Competition.Groups"] = "Grupy typowań",
                ["Competition.PredictionGroups"] = "Moduły typowań",
                ["Competition.QuestionsCount"] = "{0} pytań",
                ["Competition.MakePredictions"] = "Typuj wyniki",
                ["Competition.ReviewSaved"] = "Podgląd typów",
                ["Predictions.PageTitle"] = "Typowania",
                ["Predictions.Loading"] = "Ładowanie formularza...",
                ["Predictions.NotFoundTitle"] = "Nie znaleziono formularza",
                ["Predictions.NotFoundDescription"] = "Brak konfiguracji JSON dla tych zawodów.",
                ["Predictions.Description"] = "Odpowiedzi zapisują się lokalnie w tej przeglądarce.",
                ["Predictions.Locked"] = "Edycja typów jest zablokowana dla tych zawodów.",
                ["Predictions.Progress"] = "Postęp",
                ["Predictions.SavedAnswers"] = "Zapisane odpowiedzi",
                ["Predictions.ValidationIssues"] = "Błędy walidacji",
                ["Predictions.LastSaved"] = "Ostatni zapis",
                ["Predictions.SaveNow"] = "Zapisz teraz",
                ["Predictions.Saving"] = "Zapisywanie...",
                ["Predictions.ReviewPicks"] = "Sprawdź typy",
                ["Predictions.SavedLocally"] = "Zapisano lokalnie.",
                ["Predictions.DraftSaved"] = "Szkic zapisany lokalnie. Popraw błędy przed finalnym podglądem.",
                ["Predictions.LocalNotice"] = "MVP działa bez konta. Typy są dostępne tylko w tej przeglądarce.",
                ["Review.PageTitle"] = "Podgląd typów",
                ["Review.Loading"] = "Ładowanie podglądu...",
                ["Review.NotFoundTitle"] = "Nie znaleziono podglądu",
                ["Review.NotFoundDescription"] = "Brak konfiguracji JSON dla tych zawodów.",
                ["Review.Description"] = "Te odpowiedzi są odczytywane z lokalnego storage przeglądarki.",
                ["Review.EmptyTitle"] = "Brak zapisanych odpowiedzi",
                ["Review.EmptyDescription"] = "Przejdź do formularza typowań, żeby utworzyć lokalny szkic.",
                ["Review.ValidationIssues"] = "Ten szkic ma jeszcze {0} błędów walidacji.",
                ["Review.EditPredictions"] = "Edytuj typy",
                ["Validation.Required"] = "To pytanie jest wymagane.",
                ["Validation.ExactSelections"] = "Wybierz dokładnie {count}.",
                ["Validation.MinSelections"] = "Wybierz co najmniej {count}.",
                ["Validation.MaxSelections"] = "Wybierz maksymalnie {count}.",
                ["Validation.MinValue"] = "Wartość musi wynosić co najmniej {value}.",
                ["Validation.MaxValue"] = "Wartość nie może przekraczać {value}.",
                ["Validation.DuplicateAthletes"] = "Ten sam zawodnik nie może zostać wybrany więcej niż raz."
            },
            [AppLanguage.English] = new Dictionary<string, string>
            {
                ["App.Tagline"] = "Fantasy powerlifting",
                ["App.DataSource"] = "Static JSON data",
                ["App.Storage"] = "Local storage",
                ["Nav.Competitions"] = "Competitions",
                ["Nav.Sheffield"] = "Sheffield 2026",
                ["Nav.Worlds"] = "Worlds 2026",
                ["Nav.Menu"] = "Menu",
                ["Home.Eyebrow"] = "Predictions for powerlifting fans",
                ["Home.Title"] = "TotalCall",
                ["Home.Description"] = "Pick a meet, move through prediction modules and save your calls locally in the browser.",
                ["Home.Loading"] = "Loading competitions...",
                ["Home.Empty"] = "No competitions are configured yet.",
                ["Home.CompetitionsLabel"] = "Active configurations",
                ["Home.CompetitionsHint"] = "Each meet can define a different question set while using the same components.",
                ["Common.Starts"] = "Starts",
                ["Common.Locks"] = "Prediction lock",
                ["Common.Status"] = "Status",
                ["Common.OpenCompetition"] = "Open competition",
                ["Common.BackToCompetition"] = "Back to competition",
                ["Common.BackToCompetitions"] = "Back to competitions",
                ["Common.Tbd"] = "TBD",
                ["Common.Required"] = "Required",
                ["Common.Optional"] = "Optional",
                ["Common.NotAnswered"] = "Not answered",
                ["Common.Yes"] = "Yes",
                ["Common.No"] = "No",
                ["Common.Value"] = "Value",
                ["Common.Athlete"] = "Athlete",
                ["Common.SelectAthlete"] = "Select athlete",
                ["Common.SelectOption"] = "Select option",
                ["Common.NoValue"] = "no value",
                ["Common.Gold"] = "Gold",
                ["Common.Silver"] = "Silver",
                ["Common.Bronze"] = "Bronze",
                ["Status.Upcoming"] = "Open",
                ["Status.Locked"] = "Locked",
                ["Status.Completed"] = "Completed",
                ["Status.Archived"] = "Archived",
                ["Competition.Loading"] = "Loading competition...",
                ["Competition.NotFoundTitle"] = "Competition not found",
                ["Competition.NotFoundDescription"] = "No JSON configuration exists for this competition.",
                ["Competition.Section"] = "Competition",
                ["Competition.Federation"] = "Federation",
                ["Competition.ConfigVersion"] = "Config version",
                ["Competition.Data"] = "Data",
                ["Competition.Athletes"] = "Athletes",
                ["Competition.Categories"] = "Categories",
                ["Competition.Groups"] = "Prediction groups",
                ["Competition.PredictionGroups"] = "Prediction modules",
                ["Competition.QuestionsCount"] = "{0} question(s)",
                ["Competition.MakePredictions"] = "Make predictions",
                ["Competition.ReviewSaved"] = "Review picks",
                ["Predictions.PageTitle"] = "Predictions",
                ["Predictions.Loading"] = "Loading prediction form...",
                ["Predictions.NotFoundTitle"] = "Prediction form not found",
                ["Predictions.NotFoundDescription"] = "No JSON configuration exists for this competition.",
                ["Predictions.Description"] = "Your answers are saved locally in this browser.",
                ["Predictions.Locked"] = "Prediction editing is locked for this competition.",
                ["Predictions.Progress"] = "Progress",
                ["Predictions.SavedAnswers"] = "Saved answers",
                ["Predictions.ValidationIssues"] = "Validation issues",
                ["Predictions.LastSaved"] = "Last saved",
                ["Predictions.SaveNow"] = "Save now",
                ["Predictions.Saving"] = "Saving...",
                ["Predictions.ReviewPicks"] = "Review picks",
                ["Predictions.SavedLocally"] = "Saved locally.",
                ["Predictions.DraftSaved"] = "Draft saved locally. Fix validation issues before final review.",
                ["Predictions.LocalNotice"] = "The MVP works without an account. Picks are available only in this browser.",
                ["Review.PageTitle"] = "Prediction review",
                ["Review.Loading"] = "Loading prediction review...",
                ["Review.NotFoundTitle"] = "Prediction review not found",
                ["Review.NotFoundDescription"] = "No JSON configuration exists for this competition.",
                ["Review.Description"] = "These answers are read from local browser storage.",
                ["Review.EmptyTitle"] = "No saved answers",
                ["Review.EmptyDescription"] = "Start the prediction form to create a local draft.",
                ["Review.ValidationIssues"] = "This draft still has {0} validation issue(s).",
                ["Review.EditPredictions"] = "Edit predictions",
                ["Validation.Required"] = "This question is required.",
                ["Validation.ExactSelections"] = "Select exactly {count}.",
                ["Validation.MinSelections"] = "Select at least {count}.",
                ["Validation.MaxSelections"] = "Select no more than {count}.",
                ["Validation.MinValue"] = "Value must be at least {value}.",
                ["Validation.MaxValue"] = "Value must be no more than {value}.",
                ["Validation.DuplicateAthletes"] = "The same athlete cannot be selected more than once."
            }
        };

    public event Action? LanguageChanged;

    public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.Polish;

    public IReadOnlyList<LanguageOption> SupportedLanguages { get; } =
    [
        new LanguageOption(AppLanguage.Polish, "pl", "PL"),
        new LanguageOption(AppLanguage.English, "en", "EN")
    ];

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var storedLanguage = await localStorage.GetItemAsync(LocalStorageKeys.LanguagePreference, cancellationToken);

        if (TryParseLanguage(storedLanguage, out var language))
        {
            CurrentLanguage = language;
            LanguageChanged?.Invoke();
        }
    }

    public async Task SetLanguageAsync(AppLanguage language, CancellationToken cancellationToken = default)
    {
        if (CurrentLanguage == language)
        {
            return;
        }

        CurrentLanguage = language;
        await localStorage.SetItemAsync(LocalStorageKeys.LanguagePreference, ToCode(language), cancellationToken);
        LanguageChanged?.Invoke();
    }

    public string Text(string key)
    {
        if (Translations[CurrentLanguage].TryGetValue(key, out var translation))
        {
            return translation;
        }

        return Translations[AppLanguage.English].TryGetValue(key, out var english)
            ? english
            : key;
    }

    public string Format(string key, params object[] args)
    {
        return string.Format(Text(key), args);
    }

    public string Validation(PredictionValidationError error)
    {
        var message = Text(error.MessageKey);

        foreach (var parameter in error.Parameters)
        {
            message = message.Replace($"{{{parameter.Key}}}", parameter.Value, StringComparison.Ordinal);
        }

        return message == error.MessageKey
            ? error.Message
            : message;
    }

    private static bool TryParseLanguage(string? value, out AppLanguage language)
    {
        var normalizedValue = value?.Trim().ToLowerInvariant();

        language = normalizedValue switch
        {
            "pl" => AppLanguage.Polish,
            "en" => AppLanguage.English,
            _ => AppLanguage.Polish
        };

        return normalizedValue is "pl" or "en";
    }

    private static string ToCode(AppLanguage language)
    {
        return language switch
        {
            AppLanguage.English => "en",
            _ => "pl"
        };
    }
}
