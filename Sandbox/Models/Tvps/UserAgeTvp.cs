using System.Data;
using CaeriusNet.Mappers;

namespace CaeriusNet.Sandbox.Models.Tvps;

public sealed record UserAgeTvp(Guid Guid, short Age) : ITvpMapper<UserAgeTvp>
{
	public DataTable MapAsDataTable(IEnumerable<UserAgeTvp> items)
	{
		var dataTable = new DataTable("MyTvpUserAge");
		dataTable.Columns.Add("Guid", typeof(Guid));
		dataTable.Columns.Add("Age", typeof(short));

		foreach (var item in items) dataTable.Rows.Add(item.Guid, item.Age);

		return dataTable;
	}
}