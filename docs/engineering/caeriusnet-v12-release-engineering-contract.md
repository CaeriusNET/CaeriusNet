# Contrat d’ingénierie de la release majeure CaeriusNet 12

> Document vivant — source de vérité d’ingénierie dérivée du plan de consolidation v12.
>
> Dernière consolidation documentaire : 14 août 2026. Une ligne `Terminé`
> signifie qu’une preuve locale ou Docker identifiée a été obtenue ; elle ne
> signifie ni que les changements ont été publiés, ni qu’ils ont été poussés
> sur GitHub. Les contrôles de sortie restant à rejouer sont explicitement
> listés dans l’état vérifié.

## Objet et documents liés

Ce document fixe le périmètre, les invariants et les critères de sortie de la
release `11.2.0`. Il est destiné aux mainteneurs : ce n’est pas un guide
de migration pour un consommateur.

- [Présentation et démarrage](../../README.md)
- [Guide de migration v11 vers v12](../MIGRATION-v11-v12.md)
- [Rapport des ruptures d’API v11.1.2 vers v11.2.0](../API-BREAKS-v11-v12.md)
- [Documentation publique](../site/index.md)
- [Contributions](../../CONTRIBUTING.md)
- [Guide de validation des artefacts](../../.github/scripts/ValidatePackage.ps1)

En cas de conflit, les décisions verrouillées ci-dessous prévalent sur les
anciens exemples, noms ou comportements v11. Le guide de migration explique la
transition ; le rapport d’API en conserve la preuve de compatibilité rompue.

## Règle de nomenclature : release ≠ arborescence de code

`11.2.0` est l’identité de la release NuGet et sa future baseline de validation
d’API. Ce n’est pas un espace de noms ni une couche de code. Aucun dossier
source, test, exemple, benchmark ou outil ne doit être nommé `v12`,
`versionning`, `versions` ou équivalent.

L’organisation repose sur des *solution folders* et des répertoires décrivant
la responsabilité réelle : `src/CaeriusNet`, `src/CaeriusNet.Redis`,
`src/CaeriusNet.Aspire`, `src/CaeriusNet.Contracts`, `src/Compiler`, `tests`,
`benchmarks`, `examples`, `tools` et `docs`. À l’intérieur, les noms décrivent
le domaine (`CommandConstruction`, `ResultMaterialization`,
`TableValuedParameters`, `Caching`, etc.), jamais un numéro de version.

Cette règle est également appliquée aux générateurs produits au build : leur
emplacement et leur assembly expriment leur rôle, non la release qui les a
introduits.

## Objectifs verrouillés

| Décision | Contrat |
| --- | --- |
| Version | `11.2.0`, avec les ruptures documentées ; aucun shim `Obsolete` v11. |
| Plateforme | `net10.0`, C# `14.0`, SDK `10.0.400` avec `rollForward: latestPatch`, Roslyn `5.9.0` ou supérieur. |
| Serveur | SQL Server 2022 (major 16) et 2025 (major 17) uniquement. |
| Modèle SQL | Runtime exclusivement `CommandType.StoredProcedure` ; aucun SQL inline, EF Core, query builder ni autre moteur. |
| Exécution | Une commande « fire and forget » est attendue et sans résultat métier ; elle ne crée jamais de tâche SQL détachée. |
| Collections | Les trois formes matérialisées restent disponibles : `IEnumerable<T>`, `ReadOnlyCollection<T>`, `ImmutableArray<T>`. |
| Multi-resultats | Arity 2 à 10, pour les trois familles, avec contrat strict de nombre de jeux par défaut. |
| Caches | Cache multi-jeux atomique ; Frozen est un snapshot ; Redis exige un codec typé. |
| Bases multiples | DI simple et keyée, options nommées ; aucun état global mutable de runtime. |
| AOT | Aucun `IsAotCompatible`, `IsTrimmable` ni promesse Native AOT publiée avant preuve de toute la chaîne SqlClient/Redis/Aspire. |

## Architecture de distribution

| Package | Responsabilité exclusive | Dépendances et frontières |
| --- | --- | --- |
| `CaeriusNet` | Runtime SQL, DI, transactions, mappers, caches mémoire/Frozen, télémétrie BCL, génération DTO/TVP et analyzer. | Ne dépend pas du SDK OpenTelemetry ni de Redis/Aspire ; expose les noms de télémétrie mais garde `ActivitySource` et `Meter` privés. |
| `CaeriusNet.Redis` | Gestionnaire Redis asynchrone, codec binaire typé, intégration StackExchange.Redis. | Réutilise un unique `IConnectionMultiplexer`; le runtime ne dépend pas de cette assembly. |
| `CaeriusNet.Aspire` | Clients SQL/Redis Aspire, health checks, configuration et raccordement observabilité. | Appelle réellement les intégrations Aspire et crée une lease/scoped operation par commande. |
| `CaeriusNet.Contracts` | AutoContracts, découverte de métadonnées SQL et imports `buildTransitive`. | Seul package qui contient l’outil et les props/targets de contrat. |

Les assemblies du générateur et de l’analyzer restent distinctes. Les modèles,
parseurs JSON, règles SQL/C# et validations communes sont des fichiers source
liés, pas une dépendance binaire entre ces deux assemblies. Les signatures
multi-resultats sont générées à la construction du runtime par un générateur
build-only ; elles ne sont pas embarquées comme générateur consommateur.

## Contrat runtime SQL

### Construction et validation des commandes

- `AddCaeriusNet(...)` est idempotente ; `AddCaeriusNet("reporting", ...)`
  inscrit un `ICaeriusNetDbContext` keyé, résoluble avec
  `[FromKeyedServices("reporting")]`.
- `StoredProcedureCommand` et `StoredProcedureCommandBuilder` remplacent le
  modèle v11. Les descripteurs de paramètres sont immuables. Les
  `SqlParameter` ne sont créés qu’au moment de créer la commande SQL.
- Avant toute ouverture de connexion, vérifier schéma/procédure, doublons,
  direction, ordinal, taille, précision, échelle, timeout et type structuré.
  `AddWithValue` est interdit.
- Les TVP implémentent un contrat statique de ligne : nom SQL, métadonnées
  immuables et écriture d’une ligne dans un `SqlDataRecord`. Un record est
  réutilisé pendant l’énumération. L’API distingue une collection rejouable
  `IReadOnlyCollection<T>` d’une factory streaming
  `Func<CancellationToken,IEnumerable<T>>` ; seule la collection peut être
  cachée. Les TVP manuels portent une métadonnée complète
  `SqlServerTvpColumnAttribute` par paramètre.

### Lecture, scalaires et sorties

- Les cardinalités sont explicites : `QueryFirstAsync<T>`,
  `QueryFirstOrDefaultAsync<T>`, `QuerySingleAsync<T>` et
  `QuerySingleOrDefaultAsync<T>`.
- `ExecuteScalarAsync<T>` et `ExecuteScalarOrDefaultAsync<T>` distinguent
  absence de ligne, `DBNull`, plusieurs lignes et conversion invalide. Aucun
  cast direct de `ExecuteScalar` ne définit le contrat public.
- `ExecuteAsync` consomme complètement le reader et échoue dès qu’un résultat
  métier est présent, y compris un jeu de colonnes sans ligne. Seul
  `ExecuteNonQueryAsync` porte le nombre de lignes affectées.
- `QueryAsync<T>` est matérialisée malgré son type `IEnumerable<T>`.
  `StreamAsync<T>` ne concerne qu’un jeu de résultats, ne passe jamais par le
  cache et maintient explicitement la durée de vie du reader.
- Les API multi-resultats lisent et libèrent entièrement chaque jeu ; le nombre
  annoncé est exact par défaut. Les politiques
  `CaeriusResultSetCountPolicy` ne sont utilisées que pour une procédure
  volontairement variable. Jeux manquants ou supplémentaires sont des
  `CaeriusContractException` par défaut.
- Les sorties `OUTPUT`, `INPUTOUTPUT` et la valeur de retour sont disponibles
  après consommation complète du reader, par
  `CaeriusCommandResult<TOutput>` ou
  `CaeriusExecutionResult<TData, TOutput>` pour les contrats générés.

### Erreurs, transactions et invalidations

| Situation | Contrat d’erreur ou de durée de vie |
| --- | --- |
| Échec SQL | `CaeriusNetSqlException` contient procédure, numéro, état, classe et `ClientConnectionId`. |
| Cardinalité, forme de résultat ou schéma | `CaeriusContractException`. |
| Mapping d’une ligne ou d’un jeu | `CaeriusMappingException`, avec indices de jeu et de ligne. |
| Mutation de cache stricte | `CaeriusCacheException`. |
| Annulation | `OperationCanceledException`, jamais enveloppée. |
| Transaction | Une connexion et une transaction SQL, aucune commande concurrente, cache read-through contourné. |
| Invalidation d’écriture | Après succès hors transaction ; différée après `CommitAsync`; abandonnée au rollback. |

Les réglages applicatifs `TieredCompilation` et `TieredPGO`, ainsi que les
attributs d’optimisation non mesurés, ne font pas partie de la bibliothèque.
Une optimisation est conservée seulement avec une mesure BenchmarkDotNet
reproductible.

L’exécution de streaming annule le `SqlCommand` de manière best-effort lorsque
l’énumération est arrêtée ou annulée avant la fin. Une annulation observée avec
le jeton appelant est normalisée en `OperationCanceledException`, y compris
lorsque SqlClient remonte d’abord une exception provider. Une transaction ainsi
annulée ne réutilise pas sa commande courante.

`CommitAsync` et `RollbackAsync` observent le jeton avant de prendre possession
de l’opération terminale. Après cette frontière, l’opération SQL et les
invalidations post-commit s’achèvent sans le jeton appelant : une annulation de
requête après l’envoi du commit ne peut donc pas abandonner les invalidations et
publier durablement une écriture derrière une entrée périmée.

## Contrat cache, Redis et Aspire

### Caches injectés et clés déterministes

| Gestionnaire | Rôle | Invariant |
| --- | --- | --- |
| `IInMemoryCacheManager` | Entrées dynamiques, TTL/sliding, limite de taille, éviction et single-flight local. | Aucun état statique global. |
| `IFrozenCacheManager` | Snapshots construits en bloc puis échangés atomiquement. | Un miss ne reconstruit jamais le dictionnaire. |
| `IRedisCacheManager` | Cache distribué du package Redis. | Toutes les opérations sont async et annulables. |

`ICaeriusCacheCodec<T>` est obligatoire pour Redis. DTO et AutoContracts
génèrent des codecs binaires AOT-safe et leurs extensions DI ; un mapper manuel
en fournit un explicitement. Le payload est versionné (magic/version,
identifiant de codec, hash de contrat, bitmap de null et valeurs déterministes).
Une évolution du DTO ou du contrat change la clé : aucun ancien payload n’est
décodé au hasard.

Les clés sont centralisées et dérivent du namespace applicatif, d’une identité
stable et non secrète de la base SQL, de la procédure, du hash de contrat, de
la forme de résultat et des paramètres canoniquement encodés et hashés.
`UseSqlServer(string)` déduit cette identité de l’endpoint et du catalogue ;
une factory ou lease doit la déclarer explicitement avant d’utiliser Redis.
Une identité ressemblant à une chaîne de connexion est refusée et l’identité
elle-même est hachée avant d’entrer dans la clé. Les multi-resultats se stockent
dans un seul agrégat atomique : jamais une clé par jeu.

Le protocole de clé et de codec est binaire, versionné et déterministe. Il
encode explicitement les formes SQL usuelles (notamment mémoire binaire,
`SqlBytes`, `Guid` et `decimal`) et refuse les valeurs non représentables au
lieu de tomber sur un `ToString()` dépendant de la culture. Les bornes de
décodage évitent qu’un payload corrompu déclenche une allocation non bornée.
Les invalidations mémoire/Frozen sont linéarisées par génération afin qu’un
calcul single-flight déjà en cours ne republie pas une valeur invalidée.

### Redis et résilience

- `IConnectionMultiplexer` est un singleton réutilisé ; `IDatabase` reste
  léger. Lecture avec `StringGetLeaseAsync`, écriture en
  `ReadOnlyMemory<byte>` ; aucun `ThreadLocal<ArrayBufferWriter<byte>>`.
- Les opérations asynchrones acceptent l’annulation via `WaitAsync` et
  n’emploient pas `CommandFlags.FireAndForget`.
- Un lookup ou une population read-through en panne est journalisé, mesuré et
  laisse l’exécution continuer vers SQL. Une écriture, suppression ou
  invalidation explicite est stricte et échoue.
- Une flight locale couvre la lecture Redis, le fallback SQL et la population
  Redis pour une clé canonique. L’annulation d’un appelant cesse son attente ;
  lorsque le dernier waiter part, la factory partagée est annulée. Ce mécanisme
  n’est volontairement pas un verrou distribué.
- Payload corrompu ou codec incompatible : miss contrôlé, suppression
  best-effort et métrique dédiée ; aucune donnée cachée invalide ne remonte à
  l’appelant.

### Aspire et observabilité

- Aspire configure réellement `AddSqlServerClient`/
  `AddKeyedSqlServerClient` et `AddRedisClient`. Une lease interne crée un
  scope par opération ; une transaction conserve sa lease jusqu’à la fin.
- Le health check Caerius est optionnel et vérifie une seule fois au démarrage
  que `SERVERPROPERTY('ProductMajorVersion')` vaut 16 ou 17. Il n’y a aucun
  contrôle de version dans le chemin chaud.
- Le span client Caerius utilise les conventions SQL stables :
  `db.system.name=microsoft.sql_server`, `db.operation.name=EXECUTE`,
  `db.stored_procedure.name`, `db.namespace`, `server.address`,
  `db.response.status_code` et `error.type`. Le histogramme
  `db.client.operation.duration` est exprimé en secondes.
- En Aspire, les spans SqlClient redondants sont désactivés par défaut et
  réactivables explicitement. Les métriques cache restent à faible cardinalité
  et n’exposent ni clé ni valeur. Les valeurs de paramètres, chaînes de
  connexion et contenus TVP ne sont jamais collectés par défaut.

Les chemins sans auditeur évitent la création d’`Activity` et l’enregistrement
de métrique. Les logs de runtime, Redis et Aspire emploient les delegates
générés par `LoggerMessage` et ne propagent ni `Exception.Message`, ni chaîne
de connexion, clé de cache, paramètre ou payload.

## Contrat générateurs, Analyzer et AutoContracts

- Générateur et analyzer ciblent `Microsoft.CodeAnalysis.CSharp 5.9.0` et les
  analyzers 5.9.0 pour les consommateurs .NET 10 ; leurs tests peuvent employer le compilateur
  courant. `LangVersion` est fixé à `14.0`, jamais à `latest`.
- La sélection de constructeurs est déterministe par symboles et s’appuie sur
  exactement la même logique partagée.
- Le générateur DTO produit mapper et codec. Le générateur TVP produit les
  métadonnées privées, le writer statique et le support des ensembles vides,
  sans clone de métadonnées par appel.
- Les noms C# générés (espaces de noms, types, propriétés, mots-clés,
  identifiants Unicode et indices de colonnes) sont échappés et construits de
  façon déterministe. Les hint names sont normalisés séparément pour éviter
  les collisions de fichiers générés.
- AutoContracts utilise un manifeste v3 canonique :
  `caerius.contracts.json` contient le serveur majeur/features, les métadonnées
  découvertes, facettes/collations et jeux ordonnés ; un unique override peut
  remplacer le jeu 0 si `None`/`Undetermined` puis déclarer les jeux 1 à 9.
  Les indices sont continus, uniques et bornés de 0 à 9.
- Les hashes couvrent paramètres, directions, TVP, types SQL, facettes,
  collations, chaque jeu et l’agrégat.
  La découverte SQL est une commande de métadonnées à plusieurs jeux
  (serveur/base, table types/colonnes, procédures/paramètres, premiers jeux),
  avec filtres include/exclude, timeout, annulation et diagnostics sans secret.
  Aucune procédure n’est exécutée pendant la génération.
- Seul `CaeriusNet.Contracts` livre `buildTransitive`. Ses props/targets
  reposent sur `MSBuildThisFileDirectory`, sont opt-in pour `Pull`/`Verify`,
  privilégient un nom de connexion ou une variable d’environnement ; aucune
  chaîne de connexion brute ne passe par MSBuild.

## Packaging, API et chaîne de livraison

### Artefacts publiables

- Les quatre packages partagent version `11.2.0`, licence MIT, readme NuGet
  spécifique, icône PNG transparente 128×128, description/tags, URLs de projet
  et de dépôt, commit, Source Link, build déterministe, sources embarquées,
  symboles `.snupkg` et `PackageIcon`. Le runtime embarque uniquement Generator
  et Analyzer sous `analyzers/dotnet/cs`, chacun avec son PDB Source Link ; le
  générateur multi-résultats est déjà compilé dans `CaeriusNet.dll`.
- Central Package Management est appliqué : aucune version de package ne vit
  dans un `.csproj`; les dépendances analyzer sont `PrivateAssets=all`.
- `EnablePackageValidation`, `PublicAPI.Shipped.txt` et une baseline API font
  partie de la sortie. Le rapport v11.1.2→11.2.0 est unique ; les releases
  postérieures se comparent à `11.2.0`.
- Les 19 projets utilisent des fichiers de verrouillage NuGet et les restores
  de CI se font en `--locked-mode`. La validation Source Link est exécutée par
  script sur les PDB des artefacts packés ; elle requiert un commit distant qui
  contient les chemins réellement packés.
- Une seule construction/pacquage Release produit les artefacts à vérifier et à
  publier dans cet ordre : `CaeriusNet` → `CaeriusNet.Redis` →
  `CaeriusNet.Aspire` / `CaeriusNet.Contracts`.

### Préconditions de publication

1. La branche contenant exactement les sources publiées est committée et
   poussée ; Source Link doit être contrôlé depuis cet état distant.
2. Les suites d’intégration Docker et les consommateurs NuGet réels passent sur
   les artefacts Release uniques.
3. La baseline BenchmarkDotNet Ubuntu est enregistrée, relue et versionnée
   dans le dépôt ; aucune baseline Windows locale ne la remplace.
4. La politique NuGet Trusted Publishing associe l’organisation, le dépôt,
   `release.yml` et l’environnement GitHub `production`; le secret
   `NUGET_USER` de cet environnement est disponible.
5. Les quatre identifiants NuGet sont autorisés. La réservation du préfixe
   `CaeriusNet`, lorsqu’elle est disponible, est vérifiée.

Ces préconditions sont des critères de sortie ; elles n’autorisent pas à
publier sans instruction explicite du mainteneur.

## Matrice d’acceptation et de performance

| Domaine | Cas obligatoires | Preuve attendue |
| --- | --- | --- |
| Runtime SQL | Cardinalités, types SQL, nulls, conversion/mapping, outputs et return values. | Tests cœur et intégration SQL. |
| TVP | Vide, 1, 1 000 et flux volumineux. | SQL Server 2022 et 2025. |
| Multi-resultats | Arity 2…10 × trois collections ; jeux manquants/supplémentaires ; cache agrégé. | Tests de contrat et intégration. |
| Caches | Single-flight, TTL, snapshot Frozen, corruption/version Redis, fail-open et strict. | Tests unitaires + Redis. |
| Transactions | Commit/rollback, invalidations différées, absence de concurrence, streaming. | SQL Server 2022 et 2025. |
| DI/Aspire | DI simple et keyée, deux hosts isolés, leases, health checks, absence de double span. | Tests d’intégration Aspire. |
| Génération | Snapshots, incremental step tracking, diagnostics, SDK .NET 10 minimal. | Tests generator/analyzer. |
| Packages | Contenu, Package Validation, Source Link, quatre consommateurs sans `ProjectReference`. | Pack Release et script de validation. |
| Contracts | `Pull` génère ; `Verify` détecte le drift. | Package `CaeriusNet.Contracts` installé dans un consommateur réel. |
| Sécurité | Vulnérabilités et dépendances dépréciées. | Scan sur les quatre packages et l’outil Contracts. |

Les benchmarks couvrent mapping, scalaires, TVP (0/1/1 000/100 000), jeux
1/2/5/10, les trois collections, cache hit/miss et codecs Redis. Toute
régression de durée supérieure à 5 % ou hausse d’allocation sur un hot path est
expliquée et corrigée avant release. Un résultat sans statistiques valides
échoue ; l’outillage compare des identités de benchmark identiques.

## État vérifié au 14 août 2026

L’implémentation, la migration de l’arborescence, les tests et les exécutions
de benchmark accessibles dans le dépôt sont terminés. À cette date, les seuls
éléments qui empêchent une **publication** sont les préconditions externes
énumérées plus bas. Le backlog d’optimisation reste une feuille de route
mesurée : ce n’est pas un défaut de complétude de l’implémentation v12.

### Terminé avec preuve reçue

| Élément | État | Preuve disponible |
| --- | --- | --- |
| Consolidation fonctionnelle v12 | Terminé | Build Release froid des 19 projets sans avertissement ; 47 tests cœur, 150 générateur, 34 analyzer et 4 packaging réussis sur l’arborescence finale. |
| Packaging des quatre packages | Terminé localement | Artefacts Release uniques, contenu, readmes, icône, `.snupkg`, consommateur isolé et validation de package contrôlés. Le cœur fait environ 269 Ko ; l’outil est isolé dans Contracts. |
| AutoContracts package | Terminé et validé | Le package installé exécute `Pull`; `Verify` détecte un drift ; `buildTransitive` et outil portable validés. |
| SQL Server 2022 | Terminé et validé Docker | Cardinalités, TVP 0/1/1 000/100 000 lazy, arités 2–10, streaming, transactions, cache Redis, Aspire et Contracts exécutés sur 2022 CU26. |
| SQL Server 2025 | Terminé et validé Docker | La même matrice est exécutée sur 2025 CU7. |
| Redis | Terminé et validé Docker | Codec, corruption/version, fail-open/strict, agrégat multi-jeux et read-through 32 callers validés ; le stampede mesuré passe de 32 exécutions SQL à une. |
| Organisation non versionnée | Terminé | Les projets, tests, benchmarks, exemples, outils et documentation vivent sous des répertoires de responsabilité. Aucun chemin structurel `V12` ni ancien chemin racine n’est référencé par la documentation, les workflows ou les scripts actifs. |
| Benchmarks techniques | Terminé localement | Les catégories commande, TVP, matérialisation et cache produisent des rapports BenchmarkDotNet valides ; le runner et le comparateur refusent résultats vides, identités manquantes, régression temporelle supérieure à 5 % et allocation supplémentaire. Les plans multi-jeux statiques sont à 0 B et l’overload cache stateful retire 56 B/op sur le smoke ciblé. |
| Performance du build | Terminé localement | Restore locked 1,75 s ; build froid 14,21 s ; warm 5,01 s ; no-op 4,45 s sans `Csc`/`CoreCompile`, sur 19 projets et 64 arêtes avec parallélisme actif. |
| Gating de delivery | Terminé dans le dépôt | Les workflows et scripts couvrent restore/build/test/pack, contenu des packages, consommateurs isolés, validation d’API, sécurité, Contracts `Pull`/`Verify`, tests Docker et provenance Source Link. Leur exécution distante reste une précondition de publication. |

### Seuls bloqueurs externes de publication

| Bloqueur | Pourquoi il bloque une release légitime |
| --- | --- |
| Source Link post-commit/post-push | Les nouveaux chemins doivent être présents dans le commit distant afin que les PDB pointent vers des sources résolubles. Une vérification locale sur un état non poussé ne suffit pas. |
| Baseline BenchmarkDotNet Ubuntu | La baseline `11.2.0` doit être produite par `benchmark.yml` sur Ubuntu 24.04, examinée puis conservée dans le dépôt. |
| NuGet Trusted Publishing | La politique OIDC NuGet.org et l’environnement GitHub `production`/`NUGET_USER` nécessitent une configuration et une validation hors du dépôt. |

Docker est désormais disponible et les validations Docker SQL 2022, SQL 2025,
Redis et Contracts sont des preuves reçues. Les scans de chemins actifs
confirment également qu’aucun ancien chemin de structure ou dossier `V12` ne
subsiste dans `docs`, `.github` et les scripts. Ces résultats ne lèvent pas les
trois préconditions externes ci-dessus.

## Backlog d’optimisation approfondie, après sécurisation de la release

Ces travaux sont délibérément séparés des invariants fonctionnels et de la
sortie v12. Aucun ne doit modifier le contrat public ou ajouter des attributs
d’optimisation sans une mesure reproductible et une revue du coût de
maintenance.

1. **Chemin de commande chaud.** Mesurer allocation et copies du builder,
   validation, création de `SqlParameter`, sortie et lecture scalar ; réduire
   les allocations prouvées tout en préservant l’immuabilité.
2. **Matérialisation.** Comparer boucle reader, capacité initiale, construction
   `ReadOnlyCollection`/`ImmutableArray`, gestion `DBNull` et exceptions ;
   optimiser sans changer les diagnostics set/ligne.
3. **TVP grand flux.** Profiler le writer réutilisant `SqlDataRecord`, le coût
   de métadonnées, l’énumération synchrone/asynchrone et la pression GC à
   100 000 lignes. Évaluer les pools uniquement s’ils améliorent le profil sans
   brouiller durée de vie ou sécurité.
4. **Multi-resultats.** Vérifier les transitions `NextResultAsync`, la
   libération anticipée des jeux et l’encodage/décodage de l’agrégat cache pour
   2/5/10 jeux.
5. **Cache local.** Mesurer single-flight sous contention, locks, expiration,
   snapshots Frozen et taille mémoire. Préserver le principe : un miss Frozen
   ne reconstruit jamais implicitement.
6. **Codec Redis.** Mesurer le payload, hashage canonique, lecture en lease,
   encodage sans allocation superflue et suppression best-effort des payloads
   invalides. Ne jamais introduire de clé ou valeur à forte cardinalité en
   télémétrie.
7. **Async et annulation.** Auditer les tâches évitables, `ValueTask` dans les
   chemins synchrones prouvés, `ConfigureAwait` selon le contrat du projet et
   libération de leases/readers lors d’annulation.
8. **Générateurs et Analyzer.** Réduire le coût incremental par comparers
   stables, filtrage syntaxique précoce, strings générées et allocations Roslyn
   mesurées, sans rompre les snapshots ni la déterminisme des constructeurs.
9. **IL et JIT.** Examiner les hot paths avec les outils .NET 10 ; conserver
   des formes de code lisibles et ne garder `AggressiveInlining` ou autre hint
   qu’après gain statistiquement significatif sur la matrice BenchmarkDotNet.
10. **Paquet et build.** Mesurer l’évaluation MSBuild, les générateurs
    build-only et les consommateurs vierges ; éviter les imports globaux et les
    doublons de génération sans compromettre `buildTransitive`.

## Procédure de mise à jour de ce contrat

- Mettre à jour une ligne de statut seulement avec un lien vers un log, un test,
  un artefact ou une exécution CI identifiable.
- Noter une régression performance dans la matrice et le backlog avant de la
  masquer par un ajustement de baseline.
- Toute modification de package, surface publique, convention de télémétrie ou
  format de cache doit mettre à jour ce document, le README de package concerné
  et, le cas échéant, le guide de migration.
- Une fois `11.2.0` publié, déplacer les sujets restants vers la planification
  de la prochaine release sans créer de dossier source dépendant du numéro de
  version.
