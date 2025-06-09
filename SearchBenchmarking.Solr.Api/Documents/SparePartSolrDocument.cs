using System.Text.Json.Serialization;
using SolrNet;
using SolrNet.Commands.Parameters;
using SolrNet.Attributes;

namespace SearchBenchmarking.Solr.Api.Documents
{
    public class SparePartSolrDocument
    {
        [SolrUniqueKey("id")]
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [SolrField("sparePartNo")]
        [JsonPropertyName("sparePartNo")]
        public ICollection<string>? SparePartNo { get; set; }

        [SolrField("sparePartSerialCode")]
        [JsonPropertyName("sparePartSerialCode")]
        public ICollection<string>? SparePartSerialCode { get; set; }

        [SolrField("name")]
        [JsonPropertyName("name")]
        public ICollection<string>? Name { get; set; }

        [SolrField("description")]
        [JsonPropertyName("description")]
        public ICollection<string>? Description { get; set; }

        [SolrField("typeNo")]
        [JsonPropertyName("typeNo")]
        public ICollection<string>? TypeNo { get; set; }

        [SolrField("notes")]
        [JsonPropertyName("notes")]
        public ICollection<string>? Notes { get; set; }

        [SolrField("currency")]
        [JsonPropertyName("currency")]
        public ICollection<string>? Currency { get; set; }

        [SolrField("manufacturerNo")]
        [JsonPropertyName("manufacturerNo")]
        public ICollection<string>? ManufacturerNo { get; set; }

        [SolrField("manufacturerName")]
        [JsonPropertyName("manufacturerName")]
        public ICollection<string>? ManufacturerName { get; set; }

        [SolrField("manufacturerNotes")]
        [JsonPropertyName("manufacturerNotes")]
        public ICollection<string>? ManufacturerNotes { get; set; }

        [SolrField("categoryNo")]
        [JsonPropertyName("categoryNo")]
        public ICollection<string>? CategoryNo { get; set; }

        [SolrField("categoryName")]
        [JsonPropertyName("categoryName")]
        public ICollection<string>? CategoryName { get; set; }

        [SolrField("categoryDescription")]
        [JsonPropertyName("categoryDescription")]
        public ICollection<string>? CategoryDescription { get; set; }

        [SolrField("supplierNo")]
        [JsonPropertyName("supplierNo")]
        public ICollection<string>? SupplierNo { get; set; }

        [SolrField("supplierName")]
        [JsonPropertyName("supplierName")]
        public ICollection<string>? SupplierName { get; set; }

        [SolrField("supplierContactInfo")]
        [JsonPropertyName("supplierContactInfo")]
        public ICollection<string>? SupplierContactInfo { get; set; }

        [SolrField("supplierNotes")]
        [JsonPropertyName("supplierNotes")]
        public ICollection<string>? SupplierNotes { get; set; }

        [SolrField("locationNo")]
        [JsonPropertyName("locationNo")]
        public ICollection<string>? LocationNo { get; set; }

        [SolrField("locationName")]
        [JsonPropertyName("locationName")]
        public ICollection<string>? LocationName { get; set; }

        [SolrField("locationArea")]
        [JsonPropertyName("locationArea")]
        public ICollection<string>? LocationArea { get; set; }

        [SolrField("locationBuilding")]
        [JsonPropertyName("locationBuilding")]
        public ICollection<string>? LocationBuilding { get; set; }

        [SolrField("locationNotes")]
        [JsonPropertyName("locationNotes")]
        public ICollection<string>? LocationNotes { get; set; }

        [SolrField("unitNo")]
        [JsonPropertyName("unitNo")]
        public ICollection<string>? UnitNo { get; set; }

        [SolrField("unitName")]
        [JsonPropertyName("unitName")]
        public ICollection<string>? UnitName { get; set; }
        [SolrField("score")]
        public float Score { get; internal set; }
    }
}
