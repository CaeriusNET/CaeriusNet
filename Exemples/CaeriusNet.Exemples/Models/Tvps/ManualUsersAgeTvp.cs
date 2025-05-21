using System.Data;
using CaeriusNet.Mappers;

namespace CaeriusNet.Exemples.Models.Tvps;

public sealed record ManualUsersAgeTvp(int UsersId) : ITvpMapper<ManualUsersAgeTvp>
{
	public DataTable MapAsDataTable(IEnumerable<ManualUsersAgeTvp> items)
	{
		var dataTable = new DataTable("UsersAge");
		dataTable.Columns.Add("UsersId", typeof(int));

		foreach (var item in items) dataTable.Rows.Add(item.UsersId);

		return dataTable;
	}
}