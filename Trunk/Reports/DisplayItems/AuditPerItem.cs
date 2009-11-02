﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sitecore.Data;
using Sitecore.Data.Items;

namespace ASR.Reports.Items
{
	public class AuditPerItem : ASR.Interface.BaseScanner
	{
		public enum Mode { Item = -1, Descendants = 1, Children = 0 };
		private string _deep;
		public Mode Deep
		{
			get
			{
				if (string.IsNullOrEmpty(_deep))
				{
					_deep = getParameter("deep");
				}
				return (Mode)int.Parse(_deep);
			}
		}

		private Item _root;
		public Item Root
		{
			get
			{
				if (_root == null)
				{
					_root = Db.GetItem(getParameter("root"));
				}
				return _root;
			}
		}

		public string _allversions;
		public bool AllVersions
		{
			get
			{
				if (string.IsNullOrEmpty(_allversions))
				{
					_allversions = getParameter("allversions");
				}
				return _allversions == "1";
			}
		}

		private Database _db;
		/// <summary>
		/// Gets or sets the db.
		/// </summary>
		/// <value>The db.</value>
		protected Database Db
		{
			get
			{
				if (_db == null)
				{
					_db = Sitecore.Context.ContentDatabase == null ? 
						Sitecore.Configuration.Factory.GetDatabase("master") : Sitecore.Context.ContentDatabase;
				}
				return _db;
			}
		}

		public override ICollection Scan()
		{

			Logs.LogScanner scanner = new Logs.LogScanner();
			scanner.AddParameters(string.Format("{0}=audit", Logs.LogScanner.ENTRY_TYPES_PARAMETER));

			IEnumerable<Logs.AuditItem> auditItems = scanner.Scan().Cast<Logs.AuditItem>();

			Item[] items;
			ArrayList results = new ArrayList();
			switch (Deep)
			{
				case Mode.Descendants:
					items = Root.Axes.GetDescendants();
					break;
				case Mode.Children:
					items = Root.Axes.SelectItems("./*");
					break;
				default:
					items = new Item[] { Root };
					break;
			}

			foreach (var item in items)
			{
				if (AllVersions)
				{
					foreach (var version in item.Versions.GetVersions())
					{
						addItem(auditItems, results, version);
					}
				}
				else
				{
					addItem(auditItems, results, item);
				}
			}
			return results;
		}

		private void addItem(IEnumerable<Logs.AuditItem> auditItems, ArrayList results, Item item)
		{
			results.Add(item);
			foreach (Logs.AuditItem ai in auditItems.Where(a => compareUris(a.ItemUri, item.Uri)))
			{
				results.Add(ai);
			}
		}
		private bool compareUris(ItemUri a, ItemUri b)
		{
			if (a == null || b == null)
				return false;

			return a.DatabaseName == b.DatabaseName
				&& a.ItemID == b.ItemID
				&& a.Language == b.Language
				&& a.Version == b.Version;
		}
	}
}