namespace CaeriusNet.Exemples.Libs.Commons.Repositories;

/// <summary>
///     Data-access implementation for user-related stored procedures.
///     Split into partial files by concern:
///     <list type="bullet">
///         <item><see cref="UsersRepository" /> — base declaration (this file)</item>
///         <item><c>UsersRepository.Reads.cs</c> — single-result-set reads + cache tiers</item>
///         <item><c>UsersRepository.Tvp.cs</c> — Table-Valued Parameter reads</item>
///         <item><c>UsersRepository.MultiResultSets.cs</c> — multi-result-set reads</item>
///         <item><c>UsersRepository.Transactions.cs</c> — transactional write scenarios</item>
///     </list>
/// </summary>
public sealed partial class UsersRepository(ICaeriusNetDbContext dbContext) : IUsersRepository;
