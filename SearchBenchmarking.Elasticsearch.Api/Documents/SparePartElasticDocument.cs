using System.Text.Json.Serialization;

namespace SearchBenchmarking.Elasticsearch.Api.Documents
{
    public class SparePartElasticDocument
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("sparePartNo")]
        public string? SparePartNo { get; set; }

        [JsonPropertyName("sparePartSerialCode")]
        public string? SparePartSerialCode { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("typeNo")]
        public string? TypeNo { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("manufacturerNo")]
        public string? ManufacturerNo { get; set; }

        [JsonPropertyName("manufacturerName")]
        public string? ManufacturerName { get; set; }

        [JsonPropertyName("manufacturerNotes")]
        public string? ManufacturerNotes { get; set; }

        [JsonPropertyName("categoryNo")]
        public string? CategoryNo { get; set; }

        [JsonPropertyName("categoryName")]
        public string? CategoryName { get; set; }

        [JsonPropertyName("categoryDescription")]
        public string? CategoryDescription { get; set; }

        [JsonPropertyName("supplierNo")]
        public string? SupplierNo { get; set; }

        [JsonPropertyName("supplierName")]
        public string? SupplierName { get; set; }

        [JsonPropertyName("supplierContactInfo")]
        public string? SupplierContactInfo { get; set; }

        [JsonPropertyName("supplierNotes")]
        public string? SupplierNotes { get; set; }

        [JsonPropertyName("locationNo")]
        public string? LocationNo { get; set; }

        [JsonPropertyName("locationName")]
        public string? LocationName { get; set; }

        [JsonPropertyName("locationArea")]
        public string? LocationArea { get; set; }

        [JsonPropertyName("locationBuilding")]
        public string? LocationBuilding { get; set; }

        [JsonPropertyName("locationNotes")]
        public string? LocationNotes { get; set; }

        [JsonPropertyName("unitNo")]
        public string? UnitNo { get; set; }

        [JsonPropertyName("unitName")]
        public string? UnitName { get; set; }
    }
}
