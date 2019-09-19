<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\Microsoft.Transactions.Bridge.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\SMDiagnostics.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Configuration.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.DirectoryServices.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.EnterpriseServices.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.IdentityModel.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.IdentityModel.Selectors.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Messaging.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Net.Http.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Runtime.DurableInstancing.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Runtime.Serialization.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Security.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.ServiceModel.Activation.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.ServiceModel.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.ServiceModel.Internals.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.ServiceProcess.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Web.ApplicationServices.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Web.Services.dll</Reference>
  <Reference>&lt;RuntimeDirectory&gt;\System.Xaml.dll</Reference>
  <NuGetReference Version="9.0.1">Newtonsoft.Json</NuGetReference>
  <Namespace>System.ServiceModel</Namespace>
  <Namespace>System.ServiceModel.Channels</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>Newtonsoft.Json</Namespace>
  <Namespace>System.Runtime.Serialization</Namespace>
  <Namespace>Newtonsoft.Json.Linq</Namespace>
</Query>

void Main()
{
	var query = new DocumentQuery();
	query.DocumentTypes.AddRange("1", "5", "6", "2", "3", "4");

	query.ExcludeDocumentTypes()
		.Exclude("3", "5", "6")
		.Exclude("4", "5", "6");

	var json = JsonConvert.SerializeObject(query, Newtonsoft.Json.Formatting.Indented).Dump();

	var filterArrayDef = JObject.Parse(json)["Filters"].Value<JArray>().ToString(Newtonsoft.Json.Formatting.None).Dump();
	var newQuery = new DocumentQuery();
	newQuery.ParseAddFilters(filterArrayDef);
	newQuery.Dump();
}

// Define other methods and classes here
class DocumentQuery : ISerializable
{
	public DocumentQuery()
	{
		DocumentTypes = new List<string>();
		Filters = new List<DocumentQueryFilter>();
	}

	private DocumentQuery(SerializationInfo info, StreamingContext context)
	{
		throw new NotSupportedException();
	}

	public ICollection<string> DocumentTypes { get; }
	public ICollection<DocumentQueryFilter> Filters { get; }

	internal static T GetOrAddFilter<T>(DocumentQuery query)
		where T : DocumentQueryFilter, new()
	{
		var existingFilter = query.Filters.OfType<T>().FirstOrDefault();
		if (existingFilter != null)
		{
			return existingFilter;
		}

		var filter = new T();
		query.Filters.Add(filter);
		return filter;
	}

	public void GetObjectData(SerializationInfo info, StreamingContext context)
	{
		info.AddValue(nameof(DocumentTypes), DocumentTypes);
		info.AddValue(nameof(Filters), Filters);
	}

	public void ParseAddFilters(string filterArrayDef)
	{
		if (!(JToken.Parse(filterArrayDef) is JArray filterArray))
		{
			return;
		}

		var filters = filterArray.OfType<JObject>().Select(def => DocumentQueryFilter.FromObject(def))
		.Where(filter => filter != null);

		foreach (var filter in filters)
		{
			Filters.Add(filter);
		}
	}
}

abstract class DocumentQueryFilter : ISerializable
{
	private static readonly object EmptyParameters = new object();

	protected DocumentQueryFilter()
	{
	}

	private DocumentQueryFilter(SerializationInfo info, StreamingContext context)
	{
		throw new NotSupportedException();
	}

	public abstract string Key { get; }

	protected abstract object GetParameters();

	public void GetObjectData(SerializationInfo info, StreamingContext context)
	{
		info.AddValue(Key, GetParameters() ?? EmptyParameters);
	}

	public static DocumentQueryFilter FromObject(object filterObject)
	{
		var token = JToken.FromObject(filterObject);
		if (!(token is JObject def) || !IsDocumentQueryFilterDefinition(def))
		{
			return null;
		}

		var defProp = def.Properties().First();
		var filterKey = defProp.Name;

		switch (filterKey)
		{
			case "ExcludeDocumentTypes":
				return ExcludeDocumentTypesQueryFilter.FromParameters(defProp.Value);
			default:
				return null;
		}
	}

	private static bool IsDocumentQueryFilterDefinition(JObject def)
	{
		var properties = def.Properties();
		return properties.Count() == 1 &&
		properties.First().Value is JObject;
	}
}

class ExcludeDocumentTypesQueryFilter : DocumentQueryFilter
{
	private readonly Dictionary<string, ICollection<string>> _filterParameters;

	public ExcludeDocumentTypesQueryFilter()
	{
		_filterParameters = new Dictionary<string, ICollection<string>>(StringComparer.OrdinalIgnoreCase);
	}

	public override string Key => "ExcludeDocumentTypes";

	public ExcludeDocumentTypesQueryFilter Exclude(string typeToFind, params string[] typesToExclude)
	{
		if (!_filterParameters.ContainsKey(typeToFind))
		{
			_filterParameters.Add(typeToFind, new List<string>());
		}

		foreach (var type in typesToExclude)
		{
			_filterParameters[typeToFind].Add(type);
		}
		
		return this;
	}

	protected override object GetParameters()
	{
		return new { Exclude = _filterParameters };
	}

	internal static ExcludeDocumentTypesQueryFilter FromParameters(object filterObject)
	{
		if (!(JToken.FromObject(filterObject) is JObject parametersDef))
		{
			return null;
		}

		var filter = new ExcludeDocumentTypesQueryFilter();
		var excludeProp = parametersDef.Properties().FirstOrDefault(prop => string.Equals(prop.Name, "Exclude", StringComparison.OrdinalIgnoreCase));
		if (excludeProp != null)
		{
			var parameterValue = excludeProp.Value.ToObject<IDictionary<string, ICollection<string>>>();
			if (parameterValue != null)
			{
				foreach (var kvp in parameterValue)
				{
					filter._filterParameters.Add(kvp.Key, kvp.Value);
				}
			}
		}

		return filter;
	}
}

static class DocumentQueryExtensions
{
	public static ICollection<string> AddRange(this ICollection<string> source, params string[] items)
	{
		foreach (var item in items)
		{
			source.Add(item);
		}

		return source;
	}
	
	public static ExcludeDocumentTypesQueryFilter ExcludeDocumentTypes(this DocumentQuery query)
	{
		return DocumentQuery.GetOrAddFilter<ExcludeDocumentTypesQueryFilter>(query);
	}
}
