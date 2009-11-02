﻿using System.Collections.Generic;
using ASR.Interface;
using System.Linq;
using System;
using System.Collections;
using System.Collections.Specialized;
using Sitecore;
using ASR.DomainObjects;

namespace ASR.Interface
{
	public class Report
	{
		private Dictionary<string, FilterItem> filters;
		private Dictionary<string, ScannerItem> scanners;
		private Dictionary<string, ViewerItem> viewers;

		// buffer for the result of the scanner
		private ArrayList scannerResults;

		// buffer for filtered results i.e. scannerResults => Filter()
		private ArrayList results;

		IEnumerable<BaseViewer> _viewers;
		public IEnumerable<BaseViewer> Viewers
		{
			get
			{
				if (_viewers == null)
				{
					_viewers = viewers.Select(v => BaseViewer.Create(v.Value.FullType, v.Value.Parameters.ToString(), v.Value.ColumnsXml));
				}
				return _viewers;
			}
		}

		private List<DisplayElement> _displayElements;
		/// <summary>
		/// Gets or sets the display elements.
		/// </summary>
		/// <value>The display elements.</value>
		public List<DisplayElement> DisplayElements
		{
			get
			{
				if (_displayElements == null)
				{
					var tmp = results.OfType<object>().Select(t => IntializeDisplayElement(t));
					tmp = Sort(tmp);
					_displayElements = tmp.ToList();
				}
				return _displayElements;
			}
			set { _displayElements = value; }
		}

		private IEnumerable<DisplayElement> Sort(IEnumerable<DisplayElement> tmp)
		{
			if (SortColumns != null)
			{
				foreach (string columnName in SortColumns.Keys)
				{
					string sortOptions = SortColumns[columnName];
					Func<DisplayElement, object> columnValue = null;
					if (sortOptions.Contains("DateTime"))
					{
						columnValue = t => ParseDate(t.GetColumnValue(columnName));
					}
					else
					{
						columnValue = t => t.GetColumnValue(columnName);
					}

					if (sortOptions.Contains("ASC"))
					{
						tmp = tmp.OrderBy(columnValue);
					}
					else if (sortOptions.Contains("DESC"))
					{
						tmp = tmp.OrderByDescending(columnValue);
					}
				}
			}
			return tmp;
		}

		private object ParseDate(string value)
		{
			if (!string.IsNullOrEmpty(value))
			{
				if (DateUtil.IsIsoDate(value))
				{
					return DateUtil.IsoDateToDateTime(value);
				}
				else
				{
					DateTime result;
					DateTime.TryParse(value, out result);
					return result;
				}
			}
			return null;
		}

		private DisplayElement IntializeDisplayElement(object resultItem)
		{
			DisplayElement dElement = new DisplayElement(resultItem);
			foreach (var oViewer in Viewers)
			{
				oViewer.Display(dElement);
			}
			return dElement;
		}

		//private struct BaseObjectDefinition
		//{
		//    public BaseObjectDefinition(string type, string parameters)
		//    {
		//        this.type = type;
		//        this.parameters = parameters;
		//    }
		//    public string type;
		//    public string parameters;

		//    public override bool Equals(object obj)
		//    {
		//        if (obj is BaseObjectDefinition)
		//        {
		//            return this.Equals((BaseObjectDefinition)obj);
		//        }
		//        return false;
		//    }

		//    public override int GetHashCode()
		//    {
		//        return this.parameters.GetHashCode() ^ this.type.GetHashCode();
		//    }

		//    #region IEquatable<BaseObjectDefinition> Members

		//    bool Equals(BaseObjectDefinition other)
		//    {
		//        if (object.ReferenceEquals(other, this))
		//            return true;
		//        return other.type == this.type && other.parameters == this.parameters;
		//    }

		//    #endregion

		//    public static bool operator ==(BaseObjectDefinition obj1, BaseObjectDefinition obj2)
		//    {
		//        return obj1.Equals(obj2);
		//    }

		//    public static bool operator !=(BaseObjectDefinition obj1, BaseObjectDefinition obj2)
		//    {
		//        return !obj1.Equals(obj2);
		//    }
		//}

		public Report()
		{
			this.scanners = new Dictionary<string, ScannerItem>();
			this.viewers = new Dictionary<string, ViewerItem>();
			this.filters = new Dictionary<string, FilterItem>();
		}

		/// <summary>
		/// Add a filter to the report
		/// </summary>
		/// <param name="name">Name of the filter</param>
		/// <param name="type">Type of the class in the usual format: Fully Qualified Class Name, Assembly</param>
		/// <param name="parameters">Parameters as pipe separated: key=value|key2=value2</param>
		public void AddFilter(FilterItem filter)
		{
			if (!filters.ContainsKey(filter.Id.ToString()))
			{
				filters.Add(filter.Id.ToString(), filter);
				FlushFilterCache();
			}
		}

		/// <summary>
		/// Add a Scanner to the report
		/// </summary>
		/// <param name="name">Name of the scanner</param>
		/// <param name="type">Type of the class in the usual format: Fully Qualified Class Name, Assembly</param>
		/// <param name="parameters">Parameters as pipe separated: key=value|key2=value2</param>
		public void AddScanner(ScannerItem scanner)
		{
			Sitecore.Diagnostics.Assert.ArgumentNotNull(scanner, "scanner");

			if (!scanners.ContainsKey(scanner.Id.ToString()))
			{
				scanners.Add(scanner.Id.ToString(), scanner);
				FlushCache();
			}
		}

		/// <summary>
		/// Add a viewer to the report
		/// </summary>
		/// <param name="name">Name of the viewer</param>
		/// <param name="type">Type of the class in the usual format: Fully Qualified Class Name, Assembly</param>
		/// <param name="parameters">Parameters as pipe separated: key=value|key2=value2</param>
		public void AddViewer(ViewerItem viewer)
		{
			Sitecore.Diagnostics.Assert.ArgumentNotNull(viewer, "viewer");

			if (!viewers.ContainsKey(viewer.Id.ToString()))
			{
				viewers.Add(viewer.Id.ToString(), viewer);
			}
		}

		/// <summary>
		/// flush the buffer for the scanner, so next time the report runs, scanner will be queried again.
		/// </summary>
		public void FlushCache()
		{
			scannerResults = null;
		}

		/// <summary>
		/// Flush the cache for the filtered elements, next time the filters will be called again.
		/// </summary>
		private void FlushFilterCache()
		{
			results = null;
		}

		/// <summary>
		/// Run the report. It will only requery the Scanner if FlushCache has been used before calling this method.
		/// </summary>
		/// <param name="parameters">Not used. Only so it can be called as a ProgressBoxMethod delegate</param>
		public void Run(params object[] parameters)
		{
			if (scannerResults == null)
			{
				scannerResults = new ArrayList();
				foreach (var scanner in scanners)
				{
					BaseScanner oScanner = BaseScanner.Create(scanner.Value.FullType, scanner.Value.ReplacedAttributes);
					//TODO throw exception if null??
					scannerResults.AddRange(oScanner.Scan());
				}
			}
			Filter();
			//Sort();
		}

		/// <summary>
		/// Filters the results. All filters are "and" so an item needs to pass all filters to appear on the report.
		/// </summary>
		public void Filter()
		{
			IEnumerable<BaseFilter> oFilters = filters.Select(p => BaseFilter.Create(p.Value.FullType, p.Value.ReplacedAttributes));
			results = new ArrayList();

			foreach (var element in scannerResults)
			{
				bool add = true;
				foreach (var filter in oFilters)
				{
					if (!filter.Filter(element))
					{
						add = false;
						break;
					}
				}
				if (add)
				{
					Sitecore.Diagnostics.Assert.IsNotNull(element, "element is null ");
					results.Add(element);
				}
			}
		}

		/// <summary>
		/// Gets the results in this report. Will call Run() if it hasn't been called yet.
		/// </summary>
		/// <returns>All the results from running the report</returns>
		public IEnumerable<DisplayElement> GetResultElements()
		{
			if (results == null)
				Run();
			return GetResultElements(0, results.Count);
		}

		/// <summary>
		/// Gets only a selection of the results elements. Useful for paging.
		/// </summary>
		/// <param name="start">first result to return</param>
		/// <param name="count">number of results to return</param>
		/// <returns>Results of the report</returns>
		public IEnumerable<DisplayElement> GetResultElements(int start, int count)
		{
			int end = System.Math.Min(start + count, ResultsCount());

			return DisplayElements.Where((t, i) => (start <= i) && (i < end));
		}

		/// <summary>
		/// Total number of results.
		/// </summary>
		/// <returns>Number of results or -1 if report has not been run yet.</returns>
		public int ResultsCount()
		{
			if (results == null)
				return -1;
			return results.Count;
		}

		private NameValueCollection _sortColumns;
		protected NameValueCollection SortColumns
		{
			get
			{
				if (_sortColumns == null)
				{
					foreach (var viewer in viewers.Values)
					{
						NameValueCollection parameters = Sitecore.StringUtil.ParseNameValueCollection(viewer.ReplacedAttributes, '|', '=');
						string sortParameter = parameters["sort"];
						if (sortParameter != null)
						{
							_sortColumns = StringUtil.ParseNameValueCollection(sortParameter, '&', ',');
						}
					}
				}
				return _sortColumns;
			}
		}
	}
}
