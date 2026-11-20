# Ekstrakcja minucji metodą Crossing Number: omówienie kodu

## O co chodzi w zadaniu

**Minucje** to cechy szczególne odcisku palca, czyli miejsca, gdzie linia papilarna się kończy, dzieli albo krzyżuje. Przy rozpoznawaniu odcisków porównuje się właśnie położenie minucji.

Program wczytuje dwa odciski (A i B), znajduje na nich minucje metodą **Crossing Number**, zaznacza je kolorami i pokazuje w tabeli, ile minucji każdego typu jest na każdym odcisku. Dodatkowo usuwa fałszywe minucje, m.in. te na brzegu skanera.

## Droga od obrazu do minucji

Cała ekstrakcja jest w jednej metodzie, `FingerprintAnalyzer.Analyze`. Od niej najlepiej zacząć omawianie:

```
obraz (jasność 0-255)
  │ 1. FingerprintMask.Compute      gdzie jest odcisk, a gdzie tło skanera
  │ 2. Binarization.Apply           linie papilarne = 1, doliny = 0
  │ 3. K3M.Thin                     linie grubości 1 piksela (szkielet)
  │ 4. CrossingNumber.FindMinutiae  CN dla każdego piksela szkieletu → minucje
  │ 5. MinutiaeFilter.Apply         odrzucenie fałszywych minucji
  ▼
lista minucji + lista odrzuconych (z przyczyną)
```

W aplikacji można przełączyć **Widok** (oryginał / szkielet / binarny) i zobaczyć wynik kroków 2 i 3. Checkboxy przy typach w tabeli pokazują lub ukrywają dany typ minucji na obu obrazach.

## Pliki w kolejności omawiania

### `Minucje.Core` (algorytmy, bez interfejsu)

Każdy folder odpowiada jednemu etapowi, każda klasa i każdy enum ma osobny plik.

| Plik | Co robi |
|---|---|
| `Analysis/FingerprintAnalyzer.cs` | cały proces z kroków 1-5 w kilku linijkach |
| `Analysis/FingerprintAnalysis.cs`, `AnalysisOptions.cs` | wynik analizy i ustawienia filtra |
| `Imaging/Neighbourhood.cs` | jak są zapisane obrazy i jak czytamy 8 sąsiadów piksela |
| `Preprocessing/FingerprintMask.cs` | maska odcisku (gdzie jest odcisk, a gdzie tło skanera) |
| `Preprocessing/Binarization.cs`, `BoxBlur.cs` | binaryzacja z progiem lokalnym i rozmycie |
| `Thinning/K3M.cs` | szkieletyzacja (ta sama co w zadaniu 3) |
| `Minutiae/CrossingNumber.cs` | **główny temat zadania**: wzór CN i wyszukiwanie minucji |
| `Minutiae/MinutiaeFilter.cs` | usuwanie fałszywych minucji (punkty dodatkowe) |
| `Models/` | `Minutia`, `MinutiaType`, `RejectedMinutia`, `RejectionReason` |

### `Minucje.App` (okno WPF)

| Plik | Co robi |
|---|---|
| `MainWindow.xaml` | wygląd okna |
| `MainWindow.xaml.cs` | obsługa kontrolek: wczytaj, przelicz, narysuj, uzupełnij tabelę |
| `Rendering/MinutiaeRenderer.cs`, `ViewMode.cs` | rysuje obraz i znaczniki minucji |
| `Rendering/Legend.cs` | kolory i polskie nazwy minucji |
| `Imaging/ImageConversion.cs` | obraz → tablica jasności, zapis PNG |
| `Models/` | `FingerprintPanel` (stan panelu A/B), `TableRow` (wiersz tabeli), `SampleImage` |

## 1. Jak zapisany jest obraz (`Neighbourhood.cs`)

Obraz binarny to `byte[]`: `1` = linia, `0` = tło. Dookoła jest **ramka jednego piksela tła**, tak jak w zadaniu 3. Dzięki temu każdy piksel obrazu ma 8 sąsiadów i nie trzeba sprawdzać, czy wychodzimy poza obraz.

Sąsiadów czytamy zgodnie z ruchem wskazówek zegara:

```
P8  P1  P2
P7  P   P3
P6  P5  P4
```

Kolejność jest ważna, bo Crossing Number idzie po sąsiadach dookoła.

## 2. Maska i binaryzacja (`Preprocessing/`)

**Maska odcisku.** Obraz dzielimy na bloki 8×8 i w każdym liczymy odchylenie standardowe jasności. Na odcisku linie (ciemne) i doliny (jasne) się przeplatają, więc odchylenie jest duże. Tło skanera jest jednolite, więc odchylenie jest małe. Blok należy do odcisku, jeśli jego odchylenie przekracza 30% największego. Na koniec wypełniamy dziury: blok jest w masce, jeśli bloki z liniami są z obu stron w wierszu i z obu stron w kolumnie. Na ekranie obszar poza maską jest zabarwiony na żółto.

**Binaryzacja.** Piksel jest linią, jeśli jest **ciemniejszy niż średnia jasność w oknie 15×15 wokół niego**. To próg lokalny. Jeden próg dla całego obrazu by nie zadziałał, bo palec jest dociśnięty nierówno: jedna część odcisku jest ciemniejsza od drugiej. Wcześniej obraz jest lekko rozmyty (5×5), co skleja drobne przerwy w liniach. Obie średnie liczy ta sama funkcja `BoxBlur`, tylko z innym promieniem. Średnia jest liczona oknem przesuwanym: przy przejściu o piksel dodajemy piksel, który wchodzi do okna, i odejmujemy ten, który wychodzi, zamiast sumować całe okno od nowa.

## 3. Szkieletyzacja (`K3M.cs`)

To ten sam K3M co w zadaniu 3, przeniesiony do jednego pliku. Żeby było szybko, listy wag są zamienione na tablice 256 wartości `true/false`: sprawdzenie, czy waga jest na liście, to jeden odczyt z tablicy zamiast przeszukiwania listy. Crossing Number wymaga, żeby linia miała **dokładnie 1 piksel grubości**. Inaczej piksel w środku grubej linii miałby wielu sąsiadów i CN wychodziłby bez sensu.

## 4. Crossing Number (`CrossingNumber.cs`): najważniejsza część

Wzór:

```
CN = ½ · Σ |Pi − Pi+1|,   i = 1..8,   P9 = P1
```

W kodzie to pętla po 8 sąsiadach, w której sumujemy, ile razy wartość zmienia się między 0 a 1:

```csharp
for (int i = 0; i < 8; i++)
{
    int next = (i + 1) % 8;          // po P8 wracamy do P1
    transitions += Math.Abs(neighbours[i] - neighbours[next]);
}
return transitions / 2;
```

**Dlaczego dzielimy przez 2?** Każda linia wychodząca z piksela daje dwie zmiany: 0→1, gdy na nią wchodzimy, i 1→0, gdy z niej schodzimy. Połowa liczby zmian to więc **liczba linii wychodzących z piksela**.

| CN | Ile linii wychodzi | Typ | Znacznik |
|---|---|---|---|
| 0 | żadna | punkt izolowany | zielony okrąg |
| 1 | jedna | **zakończenie** | czerwony okrąg |
| 2 | dwie (wchodzi i wychodzi) | zwykły punkt linii, *nie minucja* | brak |
| 3 | trzy | **rozwidlenie** | niebieski okrąg |
| 4 | cztery | skrzyżowanie | fioletowy okrąg |

Przykład dla rozwidlenia (`#` = linia):

```
. . .      P8=0 P1=0 P2=0
# P #      P7=1      P3=1       ciąg P1..P8,P1: 0 0 1 0 1 0 1 0 0
. # .      P6=0 P5=1 P4=0       zmian: 6  →  CN = 3
```

`FindMinutiae` liczy CN dla każdego piksela szkieletu i zapisuje te, które są minucjami. W miejscu rozwidlenia CN = 3 mogą mieć 2-3 sąsiednie piksele, więc minucje tego samego typu w odległości do 2 px liczymy jako jedną.

## 5. Fałszywe minucje (`MinutiaeFilter.cs`): punkty dodatkowe

**Brzeg skanera.** Na granicy odcisku linie są ucięte. Każda linia dochodząca do brzegu kończy się, więc CN = 1 daje tam fałszywe zakończenie. Takich minucji jest najwięcej: około 90 na odcisk. `IsNearBorder` sprawdza kwadrat wokół minucji (domyślnie ±16 px). Jeśli jest w nim tło skanera albo krawędź obrazu, minucja jest odrzucana.

**Pary blisko siebie.** Dwie minucje bliżej niż 8 px, czyli bliżej niż odstęp między liniami, to prawie zawsze szum:

| Para | Przyczyna |
|---|---|
| zakończenie + zakończenie | przerwana linia (np. blizna, suchy palec) |
| zakończenie + rozwidlenie | ostroga (krótkie fałszywe odgałęzienie) |
| rozwidlenie + rozwidlenie | mostek między liniami albo dziura w linii |

Odrzucamy obie minucje z pary. Na obrazie odrzucone są pokazane jako szare ×, a w tabeli po prawej jest ich liczba według przyczyny. Checkbox przy przyczynie pokazuje lub ukrywa te minucje na obrazie. Suwaki zmieniają obie odległości na żywo.

## Wyniki (odciski 1_1 i 1_2, ten sam palec)

| | 1_1 | 1_2 |
|---|---|---|
| wszystkie znalezione przez CN | 156 | 152 |
| odrzucone: brzeg skanera | 87 | 91 |
| odrzucone: pary blisko siebie | 31 | 32 |
| **prawdziwe minucje** | **38** | **29** |

Prawdziwe minucje leżą w podobnych miejscach na obu odciskach, co widać, kładąc obrazy obok siebie.

## Czy to działa poprawnie? (sprawdzone na wszystkich 128 odciskach z `Fingerprints`)

| Co sprawdzono | Wynik |
|---|---|
| K3M nie przerywa ani nie skleja linii | liczba osobnych linii przed i po szkieletyzacji jest taka sama na 128/128 odciskach |
| szkielet ma grubość 1 piksela | tylko 36 miejsc z blokiem 2×2 na 128 obrazów, bez wpływu na CN |
| próg 8 px ma sens | średni odstęp między liniami wynosi 7-8,5 px, więc 8 px to jeden odstęp |
| filtr usuwa prawdziwe ostrogi i mostki, a nie przypadkowe pary | 97% odrzuconych ostróg i 99,9% mostków jest naprawdę połączonych linią szkieletu |
| wynik na odcisk | średnio 40 minucji: 76% zakończeń, 23% rozwidleń, pojedyncze skrzyżowania i punkty |

**Czego program celowo nie robi** (nie jest wymagane w zadaniu):
- **kierunku minucji** (kąt linii), który jest potrzebny dopiero przy dopasowywaniu odcisków,
- **punktów osobliwych** (rdzeń i delta), bo to cechy całego wzoru, a nie minucje,
- **wzmocnienia obrazu filtrami Gabora**. Bez niego na słabych odciskach (np. 5_3) zostaje więcej fałszywych zakończeń.

Złożone minucje (jeziorko, wysepka, ostroga, mostek) to połączenia zakończeń i rozwidleń, więc CN znajduje je jako te podstawowe typy. Jeśli są bardzo małe, filtr uznaje je za szum.

## Pytania, które mogą paść

- **Dlaczego trzeba robić szkielet?** CN zakłada linię grubości 1 piksela. Na grubej linii piksel ma za dużo sąsiadów i wynik nie odpowiada liczbie gałęzi.
- **Dlaczego próg lokalny, a nie Otsu?** Nacisk palca jest nierówny, więc jasność linii różni się w różnych częściach odcisku.
- **Skąd tyle fałszywych zakończeń?** Głównie z brzegu skanera (ucięte linie), a w środku odcisku z przerw w liniach (pory skóry, suchy palec, szum).
- **Czy filtr nie usuwa prawdziwych minucji?** Czasem tak, np. prawdziwą minucję przy samym brzegu albo dwie prawdziwe minucje leżące bardzo blisko. To kompromis, który ustawia się suwakami.
- **Czemu skrzyżowań i punktów izolowanych jest tak mało?** W prawdziwych odciskach prawie nie występują. Najczęstsze są zakończenia i rozwidlenia.
