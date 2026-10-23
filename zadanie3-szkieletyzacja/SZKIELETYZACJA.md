# Szkieletyzacja w skrócie

## Czym jest szkieletyzacja i po co się ją robi

**Szkieletyzacja** (ang. *thinning*) zamienia gruby kształt na cienką linię o grubości **1 piksela**, która zachowuje jego układ: długość, rozgałęzienia, pętle i dziury. Obiekt nie może się przy tym rozpaść na kawałki.

```
.##########.                 ............
.##########.        →        ..########..
.##########.                 ............
```

Po co to robić:
- **Odciski palców:** z grubych linii papilarnych zostają cienkie linie, w których szuka się cech szczególnych (zakończenia i rozgałęzienia linii, tzw. minucje).
- **Rozpoznawanie pisma i znaków:** grubość kreski zależy od pisaka i skanera, a kształt linii nie.
- **Mniej danych:** z całego obrazu zostaje tylko szkielet.
- **Prostsza analiza:** łatwiej policzyć zakończenia, rozgałęzienia i długość linii na cienkiej linii niż na grubej plamie.

## Ogólna zasada (taka sama we wszystkich algorytmach)

Obraz jest binarny: 1 = obiekt, 0 = tło, z ramką pustego tła dookoła (żeby czytać sąsiadów bez sprawdzania granic). Algorytm działa jak obieranie cebuli:

1. Znajdź piksele na brzegu obiektu.
2. Usuń te z nich, które **wolno** usunąć.
3. Powtarzaj, aż w całym przebiegu nic nie zostanie usunięte.

**Piksela nie wolno usunąć, gdy:**
- jest **końcem linii** (ma 0–1 sąsiada), bo szkielet by się skracał,
- jest **wnętrzem** (wszyscy sąsiedzi to obiekt), bo powstałaby dziura,
- jego usunięcie **rozerwałoby obiekt** (sąsiedzi tworzą więcej niż jedną osobną grupę).

### Waga otoczenia i tablica decyzji

Każdy z 8 sąsiadów ma wagę. Suma wag sąsiadów będących obiektem daje liczbę 0–255, która opisuje całe otoczenie piksela:

```
128  1   2
 64  x   4        przykład: sąsiedzi N, E, S  →  1 + 4 + 16 = 21
 32  16  8
```

Możliwych otoczeń jest 256, więc odpowiedź "czy wolno usunąć piksel o takim otoczeniu" ustala się **raz, na początku**, i zapisuje w tablicy `bool[256]`. W pętli głównej decyzja to jedno zajrzenie: `tablica[waga]`. Dlatego algorytmy są szybkie.

Algorytmy różnią się dwiema rzeczami:
- **skąd biorą tablice**: Zhang-Suen i Guo-Hall liczą je w kodzie z warunków, a KMM i K3M mają je gotowe z artykułów,
- **w jakiej kolejności usuwają piksele**: równolegle (zbierz, potem usuń naraz) albo sekwencyjnie (po jednym, od razu).

## Algorytmy

### 1. Zhang-Suen
Klasyczny algorytm z 1984 roku. W każdej iteracji **dwa przejścia**: pierwsze zdziera dolną i prawą stronę obiektu, drugie górną i lewą. W obu przejściach najpierw zapisujemy wszystkie piksele do usunięcia, a dopiero potem usuwamy je **naraz** (algorytm równoległy).

Tablica dla każdego z 256 otoczeń: odrzuca końce linii i wnętrza (2–6 sąsiadów), odrzuca piksele, których usunięcie rozerwałoby obiekt (dokładnie jedna grupa sąsiadów), a na końcu stosuje warunek kierunkowy (dla każdego przejścia inny), który wybiera stronę obiektu do zdarcia.

- **Zalety:** prosty, szybki, dobrze znany, daje cienką linię.
- **Wady:** może zjadać końce linii, a obiekty o grubości 2 px (np. kwadrat 2×2) potrafi usunąć **całkowicie**. Wynik bywa lekko przesunięty w jedną stronę.

### 2. Guo-Hall
Ten sam schemat co Zhang-Suen (dwa przejścia, usuwanie naraz, ta sama pętla główna), ale **inne warunki** w tablicach:
- spójność obiektu liczona przez boki (czy sąsiedzi tworzą jedną grupę),
- sąsiedzi łączeni w pary (róg + bok), a liczba par rozstrzyga, czy to koniec linii (0–1), brzeg (2–3) czy wnętrze (4),
- warunek kierunkowy: pierwsze przejście zdziera górną i prawą stronę, drugie dolną i lewą.

W praktyce Guo-Hall dokładniej rozpoznaje końce linii, więc nie skraca ich tak bardzo jak Zhang-Suen.

- **Zalety:** zachowuje więcej z cienkich fragmentów (kwadrat 2×2 nie znika), końce linii są mniej skracane, wynik jest bardziej symetryczny.
- **Wady:** zaokrągla rogi, więc zmienia kształt, i też nie gwarantuje idealnego szkieletu w każdym kształcie.

### 3. KMM (Saeed, Rybnik, Tabędzki)
Pracuje na obrazie z **etykietami**: 0 tło, 1 obiekt, 2 kontur stykający się z tłem bokiem, 3 kontur stykający się z tłem tylko rogiem. Jedna runda ma trzy kroki:

1. **Etykietowanie konturu.** Brzeg obiektu dostaje etykiety 2 i 3, a lista pikseli konturu jest zapisywana.
2. **Ścięcie narożników.** Piksele konturu o wzorcach z 2–4 sąsiadami (24 wzorce z artykułu) są usuwane od razu, wszystkie naraz.
3. **Obieranie po jednym.** Najpierw piksele z etykietą 2, potem z 3. Dla każdego liczymy wagę na aktualnym obrazie i jeśli jest w **Deletion Array z artykułu** (120 wag), piksel jest usuwany od razu. W przeciwnym razie wraca do 1.

Rundy powtarzamy, aż nic nie zostanie usunięte. Krok 2 jest po to, żeby w narożnikach nie zostawały sterczące piksele, a kolec pozostały po kroku 3 bywa usuwany przez krok 2 w następnej rundzie.

- **Zalety:** dobrze trzyma spójność, wynik jest zaokrąglony i równy.
- **Wady:** usuwanie po jednym zależy od kolejności przeglądania obrazu (od góry, od lewej), kwadrat 2×2 może zniknąć, a w trakcie pracy potrafią zostać pojedyncze kolce (znikają w kolejnych rundach).

### 4. K3M (Saeed, Tabędzki, Rybnik, Adamski)
Rozwinięcie KMM, ale na zwykłym obrazie 0/1, bez etykiet. Zamiast jednej tablicy usuwania jest ich pięć. Jedna runda:

0. **Faza 0:** zbieramy listę pikseli brzegowych (tablica A0).
1. **Fazy 1–5:** pięć razy przechodzimy po tej samej liście, za każdym razem z inną tablicą (A1…A5). Piksel jest usuwany od razu, jeśli jego aktualna waga jest w tablicy tej fazy. Tablice rosną: najpierw piksele z 3 sąsiadami, potem coraz więcej (do 7).

Po wszystkich rundach jest **faza końcowa (A1pix)**: jeden przebieg po całym obrazie, który usuwa nadmiarowe piksele (kolce, podwójne linie) i daje szkielet o grubości 1 px.

- **Zalety:** najostrożniejszy, kontur obiera się stopniowo i równomiernie, najmniej skraca końce linii, zachowuje kanciaste rogi i ma czysty wynik dzięki fazie końcowej.
- **Wady:** najbardziej złożony (7 tablic z artykułu), może zostawić nieco dłuższy lub grubszy szkielet niż pozostałe, a w drobnych kształtach zostają dodatkowe piksele.

## Przykład: ramka z dziurą 2×2

Ten sam obraz (8×6) przepuszczony przez wszystkie cztery algorytmy:

```
Wejście:      Zhang-Suen:    Guo-Hall:      KMM:           K3M:
11111111      ..####..       ........       ........       ........
11111111      .#....#.       ..####..       ..####..       .######.
111..111      .#....#.       .#....#.       .#....#.       .#....#.
111..111      .#....#.       .#....#.       .#....#.       .#....#.
11111111      .#####..       ..#..#..       ..#..#..       .######.
11111111      ........       ...##...       ...##...       ........
              15 px          12 px          12 px          16 px
```

Wszystkie zachowały dziurę i spójność. Różnią się kształtem pętli: Zhang-Suen jest lekko przesunięty i niesymetryczny, Guo-Hall i KMM zaokrąglają rogi, a K3M zachowuje prostokątny kształt.

## Czym się różnią

| | Zhang-Suen | Guo-Hall | KMM | K3M |
|---|---|---|---|---|
| Sposób usuwania | **naraz** (równolegle) | **naraz** (równolegle) | **po jednym** (sekwencyjnie) | **po jednym** (sekwencyjnie) |
| Obraz roboczy | 0/1 | 0/1 | etykiety 0–3 | 0/1 |
| Struktura rundy | 2 przejścia | 2 przejścia | 3 kroki (etykiety, narożniki, obieranie) | faza 0 + 5 faz, na końcu faza końcowa |
| Skąd reguły | warunki w kodzie | warunki w kodzie | tablice z artykułu (2) | tablice z artykułu (7) |
| Prostota | najprostszy | prosty | średni | najbardziej złożony |
| Cienkie fragmenty | 2×2 znika | 2×2 zostaje | 2×2 znika | 2×2 zostaje |
| Zachowanie końców linii | skraca | skraca mniej | średnio | skraca najmniej |
| Rogi zewnętrzne | kanciaste, wynik przesunięty | zaokrąglone | zaokrąglone | kanciaste |

Dane w wierszach o cienkich fragmentach i końcach linii pochodzą z uruchomienia naszego kodu na małych obrazach testowych (kwadrat 2×2, linia grubości 2 px).

Wspólne dla wszystkich: zachowują spójność obiektu i dziury, a obraz z milionami pikseli przetwarzają w ułamku sekundy, bo pracują na płaskiej tablicy bajtów i odczytują decyzje z gotowych tablic.

## Jak to wygląda w kodzie

- `Szkieletyzacja.Core/ThinningAlgorithm.cs`: lista algorytmów (enum z opisami).
- `Szkieletyzacja.Core/Thinning.cs`: algorytmy i budowa tablic Zhang-Suen oraz Guo-Hall.
- `Szkieletyzacja.Core/LookupTables.cs`: tablice z artykułów (KMM i K3M).
- `Szkieletyzacja.App`: aplikacja WPF (wybór obrazu, Start, czasy, podgląd wyniku).
- `Szkieletyzacja.Tests`: testy (spójność, dziury, brak zmiany wejścia, szybkość).

## Źródła

- Zhang, Suen, *A fast parallel algorithm for thinning digital patterns*, Communications of the ACM, 1984.
- Guo, Hall, *Parallel thinning with two-subiteration algorithms*, Communications of the ACM, 1989.
- Saeed, Rybnik, Tabędzki, *Implementation and Advanced Results on the Non-Interrupted Skeletonization Algorithm* (KMM), 2001.
- Saeed, Tabędzki, Rybnik, Adamski, *K3M: A universal algorithm for image skeletonization and a review of thinning techniques*, Int. J. Appl. Math. Comput. Sci., 2010, vol. 20, no. 2, s. 317–335.
