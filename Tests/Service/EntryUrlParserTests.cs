﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using VocaDb.Model.Domain;
using VocaDb.Model.Service;
using VocaDb.Model.Utils;

namespace VocaDb.Tests.Service {

	/// <summary>
	/// Unit tests for <see cref="EntryUrlParser"/>.
	/// </summary>
	[TestClass]
	public class EntryUrlParserTests {

		private const string baseUrl = "http://test.vocadb.net";
		private const string baseUrlSsl = "https://test.vocadb.net";

		private string GetAbsoluteUrl(string relative) {
			return VocaUriBuilder.MergeUrls(baseUrl, relative);
		}

		private void TestParseAbsolute(string url, int expectedId, EntryType expectedType) {
			
			var result = new EntryUrlParser(baseUrl, baseUrlSsl).Parse(GetAbsoluteUrl(url));

			Assert.AreEqual(expectedId, result.Id, "Id");
			Assert.AreEqual(expectedType, result.EntryType, "EntryType");

		}

		private void TestParseRelative(string url, int expectedId, EntryType expectedType) {
			
			var result = new EntryUrlParser(baseUrl, baseUrlSsl).Parse(url, true);

			Assert.AreEqual(expectedId, result.Id, "Id");
			Assert.AreEqual(expectedType, result.EntryType, "EntryType");

		}

		[TestMethod]
		public void HostAddressesAreSame() {
			
			var result = new EntryUrlParser(baseUrl, baseUrl).Parse(GetAbsoluteUrl("/Artist/Details/39"));
			Assert.AreEqual(39, result.Id, "Id");
			Assert.AreEqual(EntryType.Artist, result.EntryType, "EntryType");

		}

		[TestMethod]
		public void NoMatch() {
			
			TestParseAbsolute("/Search", 0, EntryType.Undefined);

		}

		[TestMethod]
		public void IdIsNotInteger() {
			
			TestParseAbsolute("/Al/undefined", 0, EntryType.Undefined);

		}

		[TestMethod]
		public void Long() {
			
			TestParseAbsolute("/Artist/Details/39", 39, EntryType.Artist);

		}

		[TestMethod]
		public void Short() {

			TestParseAbsolute("/S/39", 39, EntryType.Song);
			
		}

		[TestMethod]
		public void Short_Lowercase() {

			TestParseAbsolute("/al/39", 39, EntryType.Album);
			
		}

		[TestMethod]
		public void Relative() {
		
			TestParseRelative("/S/10", 10, EntryType.Song);
	
		}

		[TestMethod]
		public void QueryParameters() {

			TestParseAbsolute("/Al/1?pv=39", 1, EntryType.Album);
			
		}

	}

}
