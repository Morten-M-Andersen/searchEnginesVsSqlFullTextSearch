using Bogus;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.Data;
using Bogus.DataSets;

class TestDataGenerator
{
    private const string ConnectionString = "Server=localhost;Database=SparePartsDB;Trusted_Connection=True;TrustServerCertificate=True;";
    private const int Seed = 133780113;

    static async Task Main(string[] args)
    {
        Console.WriteLine($"[{DateTime.Now}] Starting test data generation...");
        try
        {
            // Opret database og tabeller først
            await CreateDatabaseAndTablesAsync();
            Console.WriteLine($"[{DateTime.Now}] Database and tables created.");

            // Skift til SIMPLE recovery model
            await ExecuteNonQueryAsync("master", "ALTER DATABASE SparePartsDB SET RECOVERY SIMPLE;");
            Console.WriteLine($"[{DateTime.Now}] Database recovery model set to SIMPLE.");

            // Disable constraints og indekser før dataindsættelse
            await DisableConstraintsAndIndexesAsync();
            Console.WriteLine($"[{DateTime.Now}] Constraints and indexes disabled.");

            // Define record counts per type
            int unitCount = 10;
            int manufacturerCountPerUnit = 20;
            int manufacturerHistoricCountPerUnit = 10;
            int categoryCountPerUnit = 30;
            int categoryHistoricCountPerUnit = 10;
            int supplierCountPerUnit = 15;
            int supplierHistoricCountPerUnit = 10;
            int locationCountPerUnit = 20;
            int locationHistoricCountPerUnit = 100;
            int componentCountPerUnitActive = 3000;
            int componentCountPerUnitHistoric = 10000;
            int sparePartCountPerUnitActive = 130000;
            int sparePartCountPerUnitHistoric = 570000;

            // Initialize Faker with English localization and Randomizer Seed
            var faker = new Faker("en");
            faker.Random = new Randomizer(Seed);

            // 1. Generate Units
            var units = GenerateUnits(unitCount, faker);
            await BulkInsertAsync("Unit", units);
            Console.WriteLine($"[{DateTime.Now}] Generated and inserted {units.Count} units.");

            // Generate data for each unit
            foreach (var unit in units)
            {
                Console.WriteLine($"[{DateTime.Now}] Generating data for unit: {unit.Name} ({unit.UnitNo})");

                // 2. Generate Manufacturers
                var manufacturers = GenerateManufacturers(manufacturerCountPerUnit, unit.Id, faker);
                await BulkInsertAsync("Manufacturer", manufacturers);
                Console.WriteLine($"[{DateTime.Now}]   Generated and inserted {manufacturers.Count} manufacturers.");

                // 2a. Generate historical Manufacturers
                var historicalManufacturers = GenerateHistoricalManufacturers(manufacturerHistoricCountPerUnit, manufacturers, unit.Id, faker);
                await BulkInsertAsync("Manufacturer", historicalManufacturers);
                Console.WriteLine($"[{DateTime.Now}]   Generated and inserted {historicalManufacturers.Count} historical manufacturers.");

                // 3. Generate Categories
                var categories = GenerateCategories(categoryCountPerUnit, unit.Id, faker);
                await BulkInsertAsync("Category", categories);
                Console.WriteLine($"[{DateTime.Now}]   Generated and inserted {categories.Count} categories.");

                // 3a. Generate historical Categories
                var historicalCategories = GenerateHistoricalCategories(categoryHistoricCountPerUnit, categories, unit.Id, faker);
                await BulkInsertAsync("Category", historicalCategories);
                Console.WriteLine($"[{DateTime.Now}]   Generated and inserted {historicalCategories.Count} historical categories.");

                // 4. Generate Suppliers
                var suppliers = GenerateSuppliers(supplierCountPerUnit, unit.Id, faker);
                await BulkInsertAsync("Supplier", suppliers);
                Console.WriteLine($"[{DateTime.Now}]   Generated and inserted {suppliers.Count} suppliers.");

                // 4a. Generate historical Suppliers
                var historicalSuppliers = GenerateHistoricalSuppliers(supplierHistoricCountPerUnit, suppliers, unit.Id, faker);
                await BulkInsertAsync("Supplier", historicalSuppliers);
                Console.WriteLine($"[{DateTime.Now}]   Generated and inserted {historicalSuppliers.Count} historical suppliers.");

                // 5. Generate Locations
                var locations = GenerateLocations(locationCountPerUnit, unit.Id, faker);
                await BulkInsertAsync("Location", locations);
                Console.WriteLine($"[{DateTime.Now}]   Generated and inserted {locations.Count} locations.");

                // 5a. Generate historical Locations
                var historicalLocations = GenerateHistoricalLocations(locationHistoricCountPerUnit, locations, unit.Id, faker);
                await BulkInsertAsync("Location", historicalLocations);
                Console.WriteLine($"[{DateTime.Now}]   Generated and inserted {historicalLocations.Count} historical locations.");

                // 6. Generate Components (active)
                var activeComponents = GenerateComponents(componentCountPerUnitActive, null, unit.Id, faker, categories.Select(c => c.Id).ToList());
                await BulkInsertAsync("Component", activeComponents);
                Console.WriteLine($"[{DateTime.Now}]   Generated and inserted {activeComponents.Count} active components.");

                // 7. Generate Components (historical)
                var historicComponents = GenerateHistoricalComponents(componentCountPerUnitHistoric, activeComponents, unit.Id, faker);
                await BulkInsertAsync("Component", historicComponents);
                Console.WriteLine($"[{DateTime.Now}]   Generated and inserted {historicComponents.Count} historical components.");

                // 8. Generate SpareParts (active)
                var activeSpareParts = GenerateSpareParts(sparePartCountPerUnitActive, null, unit.Id, manufacturers.Select(m => m.Id).ToList(), categories.Select(c => c.Id).ToList(), suppliers.Select(s => s.Id).ToList(), locations.Select(l => l.Id).ToList(), faker);
                await BulkInsertAsync("SparePart", activeSpareParts);
                Console.WriteLine($"[{DateTime.Now}]   Generated and inserted {activeSpareParts.Count} active spare parts.");

                // 9. Generate SpareParts (historical)
                var historicSpareParts = GenerateHistoricalSpareParts(sparePartCountPerUnitHistoric, activeSpareParts, unit.Id, manufacturers.Select(m => m.Id).ToList(), categories.Select(c => c.Id).ToList(), suppliers.Select(s => s.Id).ToList(), locations.Select(l => l.Id).ToList(), faker);
                await BulkInsertAsync("SparePart", historicSpareParts);
                Console.WriteLine($"[{DateTime.Now}]   Generated and inserted {historicSpareParts.Count} historical spare parts.");

                // 10. Generate ComponentPart relations
                var componentParts = GenerateComponentParts(activeComponents.Concat(historicComponents).ToList(), activeSpareParts.Concat(historicSpareParts).ToList(), unit.Id, faker);
                await BulkInsertAsync("ComponentPart", componentParts);
                Console.WriteLine($"[{DateTime.Now}]   Generated and inserted {componentParts.Count} component parts.");

                // 10a. Generate historical ComponentPart relations
                var historicalComponentParts = GenerateHistoricalComponentParts(componentParts, unit.Id, faker);
                await BulkInsertAsync("ComponentPart", historicalComponentParts);
                Console.WriteLine($"[{DateTime.Now}]   Generated and inserted {historicalComponentParts.Count} historical component parts.");

                // 11. Add edge cases for search testing
                var edgeCaseSpareParts = AddSearchEdgeCases(activeSpareParts, unit.Id, manufacturers.Select(m => m.Id).ToList(), categories.Select(c => c.Id).ToList(), suppliers.Select(s => s.Id).ToList(), locations.Select(l => l.Id).ToList(), faker);
                await BulkInsertAsync("SparePart", edgeCaseSpareParts);
                Console.WriteLine($"[{DateTime.Now}]   Generated and inserted {edgeCaseSpareParts.Count} edge case spare parts.");

                Console.WriteLine($"[{DateTime.Now}] Test data generation completed for unit: {unit.Name} ({unit.UnitNo})");
            }

            Console.WriteLine($"[{DateTime.Now}] Test data generation completed successfully!");
            Console.WriteLine($"[{DateTime.Now}] Database size: " + await GetDatabaseSizeInGBAsync() + " GB");

            Console.WriteLine($"[{DateTime.Now}] Enabling and creating Indexes.");

            // Genaktiver constraints og indekser efter dataindsættelse
            await EnableConstraintsAndIndexesAsync();
            Console.WriteLine($"[{DateTime.Now}] Constraints and indexes enabled.");

            // Skift tilbage til FULL recovery model
            await ExecuteNonQueryAsync("master", "ALTER DATABASE SparePartsDB SET RECOVERY FULL;");
            Console.WriteLine($"[{DateTime.Now}] Database recovery model set back to FULL.");

            // Opret indekser
            await CreateIndexesAsync();
            Console.WriteLine($"[{DateTime.Now}] Indexes created.");

            // Opret full-text indekser
            await CreateFullTextIndexesAsync();
            Console.WriteLine($"[{DateTime.Now}] Full-text indexes created.");
        }
        catch (SqlException ex)
        {
            Console.Error.WriteLine($"[{DateTime.Now}] Database error occurred: {ex.Message}");
            // Yderligere håndtering af SQL-specifikke fejl kan tilføjes her
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{DateTime.Now}] An unexpected error occurred: {ex.Message}");
            // Log hele undtagelsen for mere detaljer
            Console.Error.WriteLine(ex.ToString());
        }
        finally
        {
            Console.WriteLine($"[{DateTime.Now}] Test data generation process finished.");
        }
    }



    // Detailed data definitions and industry-specific lists
    // Detaljerede datadefinitioner og branchespecifikke lister


    private static readonly Dictionary<string, List<string>> IndustrySpecificComponents = new Dictionary<string, List<string>> {
        // Marine-industri komponenter
        { "Marine", new List<string> {
            "Propeller", "Rudder", "Shaft Seal", "Thruster", "Winch", "Anchor System",
            "Navigation Radar", "Communication System", "Power Generator", "Fuel Filter",
            "Ballast Pump", "Fire Suppression", "Hull Anodes", "Deck Crane", "Life Raft"
        }},
        // Energisektor komponenter
        { "Power", new List<string> {
            "Turbine Blade", "Generator Coil", "Switchgear", "Transformer", "Circuit Breaker",
            "Relay Protection", "Insulator", "Cooling System", "Control Panel", "Steam Valve",
            "Boiler Tube", "Capacitor Bank", "Excitation System", "Hydrogen Seal", "Bearing Assembly"
        }},
        // Proces-industri komponenter
        { "Process", new List<string> {
            "Centrifugal Pump", "Control Valve", "Heat Exchanger", "Pressure Vessel", "Agitator",
            "Compressor", "Reactor Vessel", "Distillation Column", "Filter Press", "Ball Mill",
            "Magnetic Separator", "Rotary Kiln", "Conveyor System", "Cyclone Separator", "Evaporator"
        }},
        // Olie & Gas komponenter
        { "Oil & Gas", new List<string> {
            "Wellhead", "BOP Stack", "Drill Bit", "Mud Pump", "Christmas Tree", "Packer",
            "Choke Manifold", "Separator Vessel", "Downhole Motor", "Wireline Tool",
            "Pipeline Pig", "Flare System", "Storage Tank", "Metering Skid", "Cathodic Protection"
        }},
        // Produktionsindustri komponenter
        { "Manufacturing", new List<string> {
            "CNC Machine", "Robotic Arm", "Conveyor Belt", "Injection Molder", "Press Brake",
            "Welding Robot", "Laser Cutter", "Packaging Machine", "Assembly Line", "Paint Booth",
            "Vibration Motor", "Sorting System", "Overhead Crane", "Industrial Oven", "SMT Machine"
        }}
    };

    private static readonly Dictionary<string, List<string>> ComponentsByCategory = new Dictionary<string, List<string>> {
        // Hydraulik-komponenter
        { "Hydraulic", new List<string> {
            "Cylinder", "Pump", "Valve", "Motor", "Filter", "Pressure Regulator",
            "Hose Assembly", "Reservoir", "Accumulator", "Seal Kit", "Flow Meter",
            "Manifold Block", "Coupling", "Pressure Gauge", "Check Valve"
        }},
        // Elektriske komponenter
        { "Electrical", new List<string> {
            "Motor", "Contactor", "Relay", "Circuit Breaker", "VFD", "Transformer",
            "PLC", "Sensor", "Switch", "Cable", "Terminal Block", "Busbar",
            "Capacitor", "Battery", "Generator", "UPS System", "Soft Starter"
        }},
        // Mekaniske komponenter
        { "Mechanical", new List<string> {
            "Bearing", "Gear", "Coupling", "Shaft", "Pulley", "Belt", "Chain",
            "Sprocket", "Gasket", "O-Ring", "Seal", "Key", "Bushing", "Spring",
            "Roller", "Guide Rail", "Lubricator", "Damper", "Vibration Isolator"
        }},
        // Pneumatiske komponenter
        { "Pneumatic", new List<string> {
            "Cylinder", "Valve", "Compressor", "Filter", "Regulator", "Lubricator",
            "Air Dryer", "Hose", "Fitting", "Pressure Switch", "FRL Unit", "Silencer",
            "Flow Control", "Quick Connect", "Manifold"
        }},
        // Instrumentering
        { "Instrumentation", new List<string> {
            "Pressure Transmitter", "Temperature Sensor", "Flow Meter", "Level Switch",
            "Gauge", "Thermocouple", "RTD", "Control Valve", "Analyzer", "Indicator",
            "Transducer", "Positioner", "Solenoid", "Actuator", "Converter", "Manometer"
        }}
    };

    // Omfattende liste af alle store producenter og deres typiske oprindelseslande
    private static readonly Dictionary<string, List<string>> ManufacturersByCountry = new Dictionary<string, List<string>> {
        { "Germany", new List<string> {
            "Siemens", "Bosch", "Festo", "Krones", "SEW Eurodrive", "Phoenix Contact",
            "Weidmüller", "Beckhoff", "Balluff", "Pilz", "Endress+Hauser", "ifm electronic",
            "JUMO", "Lenze", "Pepperl+Fuchs", "Murr Elektronik", "Schmersal", "Wika",
            "Harting", "Rittal", "Sick", "B&R", "Nord Drivesystems", "Schaeffler",
            "Krohne", "Kuka", "Schenck Process", "KSB"
        }},
        { "Denmark", new List<string> {
            "Grundfos", "Danfoss", "LINAK", "DESMI", "Maersk Container Industry",
            "LM Wind Power", "Kamstrup", "NKT", "AVK", "Welltec", "Haldor Topsoe",
            "Novo Nordisk Engineering", "Vestas", "Brüel & Kjær", "C.C. Jensen"
        }},
        { "Sweden", new List<string> {
            "ABB", "Alfa Laval", "Atlas Copco", "SKF", "Sandvik", "Ericsson",
            "Husqvarna", "ASSA ABLOY", "Hexagon", "Volvo", "Scania", "SSAB",
            "Hiab", "Nibe", "Trelleborg", "Hägglunds", "Höganäs"
        }},
        { "USA", new List<string> {
            "Emerson", "Honeywell", "Rockwell Automation", "Parker Hannifin",
            "Eaton", "GE", "Flowserve", "Johnson Controls", "Xylem", "National Instruments",
            "Caterpillar", "Cummins", "John Deere", "Lincoln Electric", "3M", "ITT",
            "Pentair", "Ingersoll Rand", "Crane", "SPX Flow", "Fisher", "Swagelok",
            "Armstrong", "Victaulic", "Bently Nevada", "Timken", "Motion Industries"
        }},
        { "Japan", new List<string> {
            "Mitsubishi Electric", "Omron", "Fanuc", "Keyence", "Yaskawa", "Yokogawa",
            "THK", "SMC", "Hitachi", "Panasonic", "KOYO", "NSK", "NTN", "Toshiba",
            "Fuji Electric", "Meidensha", "Shimadzu", "Daikin", "Ebara", "Kubota"
        }},
        { "Italy", new List<string> {
            "Camozzi", "Brevini", "Bonfiglioli", "Casappa", "Atos", "Gefran", "Carlo Gavazzi",
            "Lovato", "Eltra", "Fini", "Salvagnini", "Carraro", "Sacmi", "Siti", "Santerno"
        }},
        { "UK", new List<string> {
            "Weir", "Spirax Sarco", "Rotork", "Renishaw", "IMI", "Smiths Group",
            "Morgan Advanced Materials", "Edwards Vacuum", "Rolls-Royce", "JCB",
            "Dyson", "Delphi Technologies", "Avon Protection", "Spectris"
        }},
        { "France", new List<string> {
            "Schneider Electric", "Legrand", "Alstom", "Safran", "Nexans", "Schlumberger",
            "Zodiac Aerospace", "Saint-Gobain", "Thales", "Valeo", "Air Liquide"
        }},
        { "Switzerland", new List<string> {
            "ABB", "Schindler", "Bucher Hydraulics", "Sulzer", "Oerlikon", "Burkert",
            "Bystronic", "Leica Geosystems", "Mettler Toledo", "Bobst", "Georg Fischer"
        }},
        { "China", new List<string> {
            "Huawei", "XCMG", "Sany", "Zoomlion", "Weichai Power", "Gree Electric",
            "Midea Group", "BYD", "CRRC", "Haier", "COSCO", "Hikvision", "Lenovo"
        }},
        { "South Korea", new List<string> {
            "Samsung", "Hyundai Heavy Industries", "LG Electronics", "SK Hynix",
            "Doosan Heavy Industries", "Hanwha", "LS Electric", "Lotte Chemical", "Posco"
        }},
        { "Finland", new List<string> {
            "Wärtsilä", "Kone", "Valmet", "Metso Outotec", "Konecranes", "Vaisala",
            "Kemira", "UPM", "Stora Enso", "Ahlstrom-Munksjö", "Neste", "Cargotec"
        }},
        { "Netherlands", new List<string> {
            "Philips", "ASML", "Bosch Rexroth", "Shell", "AkzoNobel", "Stork",
            "DSM", "DAF Trucks", "VDL Group", "Nedschroef", "Van Halteren"
        }}
    };

    // Omfattende liste af materiale-typer for forskellige komponenter
    private static readonly Dictionary<string, List<string>> MaterialsByComponent = new Dictionary<string, List<string>> {
        { "Valve", new List<string> {
            "316L Stainless Steel", "304 Stainless Steel", "Cast Iron", "Ductile Iron", "Bronze",
            "Brass", "Hastelloy C276", "Hastelloy B", "Monel 400", "Inconel 625", "Titanium",
            "PVC", "CPVC", "PTFE", "PEEK", "PVDF", "Alloy 20", "Duplex 2205", "Super Duplex 2507"
        }},
        { "Pump", new List<string> {
            "Cast Iron", "316L Stainless Steel", "Cast Steel", "Ductile Iron", "Bronze", "Brass",
            "Aluminum", "Hastelloy C", "Monel", "Alloy 20", "Duplex Stainless", "Ni-Resist",
            "CD4MCu", "PTFE", "ETFE", "Viton", "EPDM", "Buna-N", "Neoprene", "Hytrel"
        }},
        { "Seal", new List<string> {
            "Nitrile (NBR)", "Viton (FKM)", "EPDM", "Silicone", "PTFE", "Neoprene", "Polyurethane",
            "Kalrez", "Graphite", "Carbon", "Ceramic", "Tungsten Carbide", "Silicon Carbide",
            "Aflas", "Hypalon", "Butyl", "TFE/P", "Buna-N", "Chemraz", "Garlock"
        }},
        { "Bearing", new List<string> {
            "Chrome Steel", "Stainless Steel", "Carbon Steel", "Brass", "Bronze", "Plastic",
            "Ceramic", "Graphite", "PTFE", "Nylon", "Carbon", "Acetal", "Rulon", "Vespel",
            "Torlon", "Aluminum Bronze", "Babbitt Metal"
        }},
        { "Motor", new List<string> {
            "Carbon Steel", "Aluminum", "Copper", "Stainless Steel", "Cast Iron", "Silicon Steel",
            "Insulation Class F", "Insulation Class H", "Neodymium Magnets", "Ferrite Magnets",
            "Polypropylene", "Polyester", "Epoxy", "Samarium Cobalt"
        }},
        { "Pipeline", new List<string> {
            "Carbon Steel API 5L", "Stainless Steel 304/304L", "Stainless Steel 316/316L",
            "Duplex 2205", "Super Duplex 2507", "HDPE", "PVC", "CPVC", "FRP", "GRE",
            "Concrete Steel Cylinder", "Ductile Iron", "Cast Iron", "Copper", "Inconel", "Monel"
        }}
    };

    // Domænespecifikke lister for komponentrelaterede data
    private static readonly Dictionary<string, List<string>> RealisticComponentPositions = new Dictionary<string, List<string>> {
        { "Pump", new List<string> {
            "Suction Side", "Discharge Side", "Front Cover", "Rear Cover", "Drive End",
            "Non-Drive End", "Impeller Housing", "Shaft Assembly", "Mechanical Seal Area",
            "Coupling End", "Bearing Housing", "Volute", "Motor Adapter"
        }},
        { "Motor", new List<string> {
            "Drive End", "Non-Drive End", "Terminal Box", "Cooling Fan", "Stator Housing",
            "Rotor Assembly", "Front Bearing", "Rear Bearing", "Encoder Mount", "Shaft End"
        }},
        { "Valve", new List<string> {
            "Inlet Port", "Outlet Port", "Valve Body", "Bonnet", "Stem Assembly", "Seat Area",
            "Actuator Connection", "Lower Flange", "Upper Flange", "Packing Area"
        }},
        { "Gearbox", new List<string> {
            "Input Shaft", "Output Shaft", "Housing", "Top Cover", "Bottom Section",
            "First Stage", "Second Stage", "Third Stage", "Oil Sump", "Cooling Section"
        }},
        { "Hydraulic System", new List<string> {
            "Reservoir", "Pump Section", "Control Block", "Cylinder Connection", "Return Line",
            "Pressure Line", "Filter Housing", "Accumulator Mount", "Valve Bank", "Level Indicator"
        }},
        { "Electrical Cabinet", new List<string> {
            "Upper Section", "Lower Section", "Door", "Back Plate", "DIN Rail A1",
            "DIN Rail B2", "Cable Duct Left", "Cable Duct Right", "Power Section", "Control Section"
        }}
    };

    // Produktfamilier og betegnelser
    private static readonly Dictionary<string, List<string>> ProductFamilies = new Dictionary<string, List<string>> {
        { "Pumps", new List<string> {
            "CP Series", "GT Series", "Multistage 5000", "Process Line X", "SlurryMaster",
            "ChemLine", "MarinePump", "API Standard", "LowFlow Series", "VerticalCan"
        }},
        { "Valves", new List<string> {
            "V-Control", "SteelSeat Pro", "Butterfly 3000", "High-Pressure Series",
            "SafeClose", "TrunnionMount", "EasyFlow", "RegControl", "ChemControl", "MarineValve"
        }},
        { "Motors", new List<string> {
            "IE3 Premium", "Hazardous Area Series", "Marine Duty", "VectorDrive",
            "Cooling Tower Series", "EFF1 Series", "Inverter Duty", "High-Torque",
            "Explosion-Proof", "Brake Motor Series"
        }},
        { "Bearings", new List<string> {
            "Long-Life Series", "HighTemp", "Self-Aligning", "SplitCase", "SleeveType",
            "PrecisionLine", "Heavy Load", "Oil Lubricated", "Water Resistant", "Ceramic Hybrid"
        }},
        { "Seals", new List<string> {
            "DualSafe", "TempMaster", "ChemShield", "SlurryGuard", "SplitSeal",
            "HighPressure", "API Plan 53", "DryRunning", "GasSeal", "CartridgeType"
        }}
    };

    // Expanded list of common search terms
    private static readonly List<string> CommonSearchTerms = new List<string> {
        // Original terms
        "overhaul kit", "repair kit", "seal kit", "gasket set", "bearing assembly",
        "replacement filter", "high performance", "high temperature", "corrosion resistant",
        "heavy duty", "emergency spare", "critical part", "OEM equivalent", "recommended spare",
        "preventive maintenance", "outage spare", "long lead time", "offshore rated", "ATEX approved",
        
        // Additional industry-specific terms
        "stainless steel", "carbon steel", "duplex", "hastelloy", "inconel", "monel",
        "bronze", "brass", "titanium", "aluminum", "cast iron", "ductile iron",
        
        // Condition-related terms
        "new", "refurbished", "reconditioned", "rebuilt", "repaired", "used", "NOS",
        "factory sealed", "OEM", "aftermarket", "genuine", "compatible", "equivalent",
        
        // Application-specific terms
        "offshore", "marine", "oil and gas", "chemical", "pharmaceutical", "food grade",
        "power generation", "water treatment", "mining", "pulp and paper", "cement",
        
        // Technical specifications
        "high pressure", "low pressure", "high temperature", "cryogenic", "abrasive service",
        "corrosive", "sanitary", "aseptic", "explosion proof", "intrinsically safe",
        
        // Certification and standards
        "ISO", "API", "ASME", "ANSI", "DIN", "JIS", "BS", "NACE", "CE marked", "PED",
        "ATEX", "IECEx", "FDA", "3-A", "USP", "NSF", "ABS", "DNV", "Lloyd's", "BV",
        
        // Maintenance terminology
        "predictive maintenance", "condition monitoring", "reliability", "MTBF",
        "failure mode", "root cause analysis", "vibration analysis", "thermal imaging",
        
        // Industry abbreviations
        "BOM", "MRO", "EAM", "CMMS", "PM", "WO", "RFQ", "PO", "ETA", "MOQ",
        "FOB", "CIF", "EXW", "DAP", "ROI", "TCO", "NPT", "BSPT", "BSP", "ASME",
        
        // Misspelled common terms (testing search robustness)
        "maintainence", "hydralic", "pnuematic", "stainles", "replacment", "compressor",
        
        // Part categorization
        "rotating equipment", "static equipment", "instrumentation", "electrical",
        "mechanical", "hydraulic", "pneumatic", "structural", "piping", "insulation",
        
        // Industry-specific part categories
        "PSV", "PRV", "MOV", "transmitter", "solenoid", "actuator", "transducer",
        "RTD", "thermocouple", "indicator", "controller", "analyzer", "gauge", "meter"
    };

    // Generer Unit data
    static List<Unit> GenerateUnits(int count, Faker faker)
    {
        var units = new List<Unit>();

        var unitNames = new[] {
            "Marine Division", "Power Generation", "Offshore Platform",
            "Production Plant", "Process Facility", "Testing test",
            "Large Vessel", "Medium Vessel", "Small Vessel", "Marine Vessel"
        };

        for (int i = 0; i < count; i++)
        {
            units.Add(new Unit
            {
                Id = Guid.NewGuid(),
                UnitNo = $"UNIT-{i + 1:D3}",
                Name = i < unitNames.Length ? unitNames[i] : $"Unit {i + 1}",
                Description = faker.Company.CatchPhrase(),
                IsActive = true
            });
        }

        return units;
    }

    // Generer producenter med realistiske navne og lande
    static List<Manufacturer> GenerateManufacturers(int count, Guid unitGuid, Faker faker)
    {
        var manufacturers = new List<Manufacturer>();
        int index = 0;

        // For hvert land, tilføj nogle af deres producenter
        foreach (var countryManufacturers in ManufacturersByCountry)
        {
            string country = countryManufacturers.Key;
            List<string> mfrList = countryManufacturers.Value;

            // Vælg et antal producenter fra dette land baseret på landets størrelse i industrien
            int countryCount = Math.Min(
                mfrList.Count,
                (int)Math.Ceiling(count * GetCountryImportance(country) / 100.0)
            );

            // Tilføj disse producenter til listen
            for (int i = 0; i < countryCount && index < count; i++, index++)
            {
                manufacturers.Add(new Manufacturer
                {
                    Id = Guid.NewGuid(),
                    master_id = null,
                    UnitGuid = unitGuid,
                    ManufacturerNo = $"MFR-{index + 1:D3}",
                    Name = i < mfrList.Count ? mfrList[i] : faker.Company.CompanyName(),
                    Country = country,
                    Notes = faker.Random.Bool(0.7f) ? GenerateManufacturerNotes(faker) : null,
                    CreatedDate = DateTime.Now.AddDays(-faker.Random.Int(1, 365)), // Sæt CreatedDate her for aktive
                    LastModifiedDate = faker.Random.Bool(0.7f) ? DateTime.Now.AddDays(-faker.Random.Int(0, 30)) : null
                });
            }
        }

        // Fyld op med tilfældige producenter hvis nødvendigt
        while (manufacturers.Count < count)
        {
            var country = faker.PickRandom(ManufacturersByCountry.Keys.ToArray());
            manufacturers.Add(new Manufacturer
            {
                Id = Guid.NewGuid(),
                master_id = null,
                UnitGuid = unitGuid,
                ManufacturerNo = $"MFR-{manufacturers.Count + 1:D3}",
                Name = faker.Company.CompanyName(),
                Country = country,
                Notes = faker.Random.Bool(0.7f) ? GenerateManufacturerNotes(faker) : null,
                CreatedDate = DateTime.Now.AddDays(-faker.Random.Int(1, 365)), // Sæt CreatedDate her for aktive
                LastModifiedDate = faker.Random.Bool(0.7f) ? DateTime.Now.AddDays(-faker.Random.Int(0, 30)) : null
            });
        }

        return manufacturers;
    }

    // Generate historical Manufacturers
    static List<Manufacturer> GenerateHistoricalManufacturers(
        int count,
        List<Manufacturer> activeManufacturers,
        Guid unitGuid,
        Faker faker)
    {
        var historicalManufacturers = new List<Manufacturer>();

        // Vælg tilfældige aktive producenter til at få historik
        var selectedManufacturers = faker.Random.ListItems(activeManufacturers.ToList(), Math.Min(count, activeManufacturers.Count));

        foreach (var manufacturer in selectedManufacturers)
        {
            // Generer 1 historisk version
            DateTime historicalDate = manufacturer.CreatedDate.AddDays(-faker.Random.Int(180, 1095));
            DateTime? historicalLastModifiedDate = historicalDate.AddDays(faker.Random.Int(1, 30));

            // Sikr at historiske datoer ikke er før 1753 (selvom de burde være det nu)
            if (historicalDate < new DateTime(1753, 1, 1))
            {
                historicalDate = new DateTime(1753, 1, 1).AddDays(faker.Random.Int(0, 30));
                historicalLastModifiedDate = historicalDate.AddDays(faker.Random.Int(1, 30));
            }
            else if (historicalLastModifiedDate.HasValue && historicalLastModifiedDate.Value < new DateTime(1753, 1, 1))
            {
                historicalLastModifiedDate = new DateTime(1753, 1, 2).AddDays(faker.Random.Int(0, 30));
            }


            var historicalManufacturer = new Manufacturer
            {
                Id = Guid.NewGuid(),
                master_id = manufacturer.Id,
                UnitGuid = unitGuid,
                ManufacturerNo = manufacturer.ManufacturerNo,
                Name = manufacturer.Name, // Samme navn
                Country = manufacturer.Country, // Samme land
                Notes = manufacturer.Notes != null ?
                    $"Previous contact information. {faker.Company.CatchPhrase()}" : null,
                CreatedDate = historicalDate,
                LastModifiedDate = historicalLastModifiedDate
            };

            historicalManufacturers.Add(historicalManufacturer);
        }

        return historicalManufacturers;
    }

    // Hjælpemetode til at vurdere et lands vigtighed i industriel produktion (cirka vægtning)
    static double GetCountryImportance(string country)
    {
        switch (country)
        {
            case "Germany": return 20.0;
            case "USA": return 20.0;
            case "Japan": return 12.0;
            case "China": return 10.0;
            case "Italy": return 8.0;
            case "UK": return 7.0;
            case "Sweden": return 6.0;
            case "Switzerland": return 5.0;
            case "France": return 5.0;
            case "South Korea": return 3.0;
            case "Finland": return 2.0;
            case "Denmark": return 2.0;
            case "Netherlands": return 2.0;
            default: return 1.0;
        }
    }

    // Generer kategorier med hierarkiske relationer
    static List<Category> GenerateCategories(int count, Guid unitGuid, Faker faker)
    {
        var categories = new List<Category>();
        var categoryRelations = new Dictionary<string, List<string>>();
        int index = 0;

        DateTime minDate = new DateTime(1753, 1, 1);
        DateTime maxDate = DateTime.Now;

        // Først generer hovedkategorier (baseret på vores definerede komponentsystemer)
        foreach (var categorySystem in ComponentsByCategory.Keys)
        {
            if (index < count)
            {
                var categoryId = Guid.NewGuid();
                categories.Add(new Category
                {
                    Id = categoryId,
                    master_id = null,
                    UnitGuid = unitGuid,
                    CategoryNo = $"CAT-{index + 1:D3}",
                    Name = categorySystem,
                    Description = $"Main category for {categorySystem.ToLower()} components and parts",
                    CreatedDate = GenerateRandomDate(faker, minDate, maxDate), // Tilføjet dato
                    LastModifiedDate = GenerateNullableRandomDate(faker, minDate, maxDate, 0.6f) // Tilføjet dato
                });
                categoryRelations[categorySystem] = new List<string>();
                index++;
            }
        }

        // Derefter generer underkategorier til hver hovedkategori
        foreach (var category in ComponentsByCategory)
        {
            string mainCategory = category.Key;
            var subCategories = category.Value;
            var mainCategoryObj = categories.FirstOrDefault(c => c.Name == mainCategory);
            if (mainCategoryObj != null)
            {
                foreach (var subCategory in subCategories)
                {
                    if (index < count)
                    {
                        categories.Add(new Category
                        {
                            Id = Guid.NewGuid(),
                            master_id = null,
                            UnitGuid = unitGuid,
                            CategoryNo = $"CAT-{index + 1:D3}",
                            Name = $"{mainCategory} - {subCategory}",
                            Description = $"Subcategory for {subCategory.ToLower()} within {mainCategory.ToLower()} systems",
                            CreatedDate = GenerateRandomDate(faker, minDate, maxDate), // Tilføjet dato
                            LastModifiedDate = GenerateNullableRandomDate(faker, minDate, maxDate, 0.6f) // Tilføjet dato
                        });
                        categoryRelations[mainCategory].Add(subCategory);
                        index++;
                    }
                }
            }
        }

        // Hvis vi stadig mangler kategorier, tilføj flere detaljerede underkategorier
        while (index < count)
        {
            var existingCat = faker.PickRandom(categories.Where(c => c.Name.Contains(" - ")).ToList());
            if (existingCat != null)
            {
                var variant = faker.Commerce.ProductAdjective();
                categories.Add(new Category
                {
                    Id = Guid.NewGuid(),
                    master_id = null,
                    UnitGuid = unitGuid,
                    CategoryNo = $"CAT-{index + 1:D3}",
                    Name = $"{existingCat.Name} - {variant}",
                    Description = $"Specialized {variant.ToLower()} variant of {existingCat.Name.ToLower()}",
                    CreatedDate = GenerateRandomDate(faker, minDate, maxDate), // Tilføjet dato
                    LastModifiedDate = GenerateNullableRandomDate(faker, minDate, maxDate, 0.6f) // Tilføjet dato
                });
                index++;
            }
            else
            {
                var categoryName = $"{faker.Commerce.Department()} Parts";
                categories.Add(new Category
                {
                    Id = Guid.NewGuid(),
                    master_id = null,
                    UnitGuid = unitGuid,
                    CategoryNo = $"CAT-{index + 1:D3}",
                    Name = categoryName,
                    Description = $"General category for {categoryName.ToLower()}",
                    CreatedDate = GenerateRandomDate(faker, minDate, maxDate), // Tilføjet dato
                    LastModifiedDate = GenerateNullableRandomDate(faker, minDate, maxDate, 0.6f) // Tilføjet dato
                });
                index++;
            }
        }
        return categories;
    }

    static List<Category> GenerateHistoricalCategories(
        int count,
        List<Category> activeCategories,
        Guid unitGuid,
        Faker faker)
    {
        var historicalCategories = new List<Category>();
        DateTime minHistoricalDate = new DateTime(1753, 1, 1).AddDays(30);  // Sikrer lidt margin efter 1753


        var selectedCategories = faker.Random.ListItems(activeCategories.ToList(), Math.Min(count, activeCategories.Count));

        foreach (var category in selectedCategories)
        {
            DateTime historicalDate = category.CreatedDate.AddDays(-faker.Random.Int(180, 1095));
            DateTime? historicalLastModifiedDate = historicalDate.AddDays(faker.Random.Int(1, 30));

            // Sikkerhedscheck for historiske datoer
            if (historicalDate < minHistoricalDate)
            {
                historicalDate = minHistoricalDate.AddDays(faker.Random.Int(0, 30));
                historicalLastModifiedDate = historicalDate.AddDays(faker.Random.Int(1, 30));
            }
            else if (historicalLastModifiedDate.HasValue && historicalLastModifiedDate.Value < minHistoricalDate)
            {
                historicalLastModifiedDate = minHistoricalDate.AddDays(faker.Random.Int(1, 30));
            }

            historicalCategories.Add(new Category
            {
                Id = Guid.NewGuid(),
                master_id = category.Id,
                UnitGuid = unitGuid,
                CategoryNo = category.CategoryNo,
                Name = category.Name,
                Description = category.Description != null ? $"Previous classification. {faker.Commerce.ProductAdjective()} {category.Name.ToLower()}." : null,
                CreatedDate = historicalDate,
                LastModifiedDate = historicalLastModifiedDate
            });
        }
        return historicalCategories;
    }

    // Generer leverandører med realistiske navne og information
    static List<Supplier> GenerateSuppliers(int count, Guid unitGuid, Faker faker)
    {
        var suppliers = new List<Supplier>();

        // Realistiske leverandørtyper
        var supplierTypes = new[] {
         "Manufacturer Direct", "Authorized Distributor", "Wholesale Supplier",
         "Specialized Parts", "Industrial Supply", "Marine Supply", "Technical Parts",
         "OEM Partner", "Global Supply", "Local Distributor", "Technical Services",
         "Spare Parts Specialist", "After-Market", "MRO Supplier"
     };

        DateTime minDate = new DateTime(1753, 1, 1);
        DateTime maxDate = DateTime.Now;

        for (int i = 0; i < count; i++)
        {
            string supplierType = supplierTypes[i % supplierTypes.Length];
            string name = $"{faker.Company.CompanyName()} {supplierType}";

            suppliers.Add(new Supplier
            {
                Id = Guid.NewGuid(),
                master_id = null,
                UnitGuid = unitGuid,
                SupplierNo = $"SUP-{i + 1:D3}",
                Name = name,
                ContactInfo = GenerateSupplierContactInfo(faker),
                Notes = faker.Random.Bool(0.6f) ? GenerateSupplierNotes(faker, supplierType) : null,
                CreatedDate = GenerateRandomDate(faker, minDate, maxDate), // Tilføjet dato
                LastModifiedDate = GenerateNullableRandomDate(faker, minDate, maxDate, 0.5f)  // Tilføjet dato
            });
        }

        return suppliers;
    }

    // Generate historical Suppliers
    static List<Supplier> GenerateHistoricalSuppliers(
        int count,
        List<Supplier> activeSuppliers,
        Guid unitGuid,
        Faker faker)
    {
        var historicalSuppliers = new List<Supplier>();

        // Tilføj datoer til alle aktive leverandører
        foreach (var activeSupplier in activeSuppliers)
        {
            activeSupplier.CreatedDate = DateTime.Now.AddDays(-faker.Random.Int(1, 365));
            activeSupplier.LastModifiedDate = faker.Random.Bool(0.8f) ?
                DateTime.Now.AddDays(-faker.Random.Int(0, 45)) : null;
        }

        // Vælg tilfældige aktive leverandører til at få historik
        var selectedSuppliers = faker.Random.ListItems(activeSuppliers.ToList(), Math.Min(count, activeSuppliers.Count));

        foreach (var supplier in selectedSuppliers)
        {
            // Generer 1 historisk version
            DateTime historicalDate = supplier.CreatedDate.AddDays(-faker.Random.Int(180, 1095));

            var historicalSupplier = new Supplier
            {
                Id = Guid.NewGuid(),
                master_id = supplier.Id,
                UnitGuid = unitGuid,
                SupplierNo = supplier.SupplierNo,
                Name = supplier.Name, // Samme navn
                                      // Typisk ændring vil være kontaktinformation
                ContactInfo = GenerateOldContactInfo(supplier.ContactInfo, faker),
                Notes = supplier.Notes != null ?
                    $"Previous supplier terms. {faker.Commerce.ProductAdjective()} delivery times." : null,

                CreatedDate = historicalDate,
                LastModifiedDate = historicalDate.AddDays(faker.Random.Int(1, 30))
            };

            historicalSuppliers.Add(historicalSupplier);
        }

        return historicalSuppliers;
    }

    // Generer lokationer med realistiske navne og information

    // Hjælper til at generere gammel kontaktinformation
    static string GenerateOldContactInfo(string currentContactInfo, Faker faker)
    {
        if (string.IsNullOrEmpty(currentContactInfo))
            return faker.Name.FullName() + ", " + faker.Phone.PhoneNumber();

        // Split kontaktinfo i dele
        var parts = currentContactInfo.Split(',');

        if (parts.Length >= 3)
        {
            // Ændre navn eller telefonnummer
            if (faker.Random.Bool())
                parts[0] = faker.Name.FullName(); // Ny kontaktperson
            else
                parts[1] = " " + faker.Phone.PhoneNumber(); // Nyt telefonnummer
        }

        return string.Join(",", parts);
    }

    static List<Location> GenerateLocations(int count, Guid unitGuid, Faker faker)
    {
        var locations = new List<Location>();
        var areas = new[] { "North", "South", "East", "West", "Central", "Upper Deck", "Lower Deck", "Main Deck", "Process Area", "Production", "Warehouse", "Technical" };
        var buildings = new[] { "Warehouse", "Production Hall", "Main Building", "Workshop", "Storage Area", "Engine Room", "Control Room", "Technical Room", "Assembly Shop", "Maintenance Building", "Logistics Center" };

        DateTime minDate = new DateTime(1753, 1, 1);
        DateTime maxDate = DateTime.Now;

        for (int i = 0; i < count; i++)
        {
            string area = faker.PickRandom(areas);
            string building = faker.PickRandom(buildings);
            int number = faker.Random.Number(1, 9);
            locations.Add(new Location
            {
                Id = Guid.NewGuid(),
                master_id = null,
                UnitGuid = unitGuid,
                LocationNo = $"LOC-{i + 1:D3}",
                Name = $"{area} {building} {number}",
                Area = area,
                Building = building,
                Notes = faker.Random.Bool(0.4f) ? GenerateLocationNotes(faker) : null,
                CreatedDate = GenerateRandomDate(faker, minDate, maxDate),  // Tilføjet dato
                LastModifiedDate = GenerateNullableRandomDate(faker, minDate, maxDate, 0.5f)   // Tilføjet dato
            });
        }
        return locations;
    }

    // Generate historical Locations
    static List<Location> GenerateHistoricalLocations(
        int count,
        List<Location> activeLocations,
        Guid unitGuid,
        Faker faker)
    {
        var historicalLocations = new List<Location>();
        DateTime minHistoricalDate = new DateTime(1753, 1, 1).AddDays(30);

        foreach (var activeLocation in activeLocations)
        {
            // Ingen grund til at sætte datoer her
        }

        var selectedLocations = faker.Random.ListItems(activeLocations.ToList(), Math.Min(count, activeLocations.Count));

        foreach (var location in selectedLocations)
        {
            DateTime historicalDate = location.CreatedDate.AddDays(-faker.Random.Int(180, 1095));
            DateTime? historicalLastModifiedDate = historicalDate.AddDays(faker.Random.Int(1, 30));

            // Sikkerhedscheck
            if (historicalDate < minHistoricalDate)
            {
                historicalDate = minHistoricalDate.AddDays(faker.Random.Int(0, 30));
                historicalLastModifiedDate = historicalDate.AddDays(faker.Random.Int(1, 30));
            }
            else if (historicalLastModifiedDate.HasValue && historicalLastModifiedDate.Value < minHistoricalDate)
            {
                historicalLastModifiedDate = minHistoricalDate.AddDays(faker.Random.Int(1, 30));
            }

            historicalLocations.Add(new Location
            {
                Id = Guid.NewGuid(),
                master_id = location.Id,
                UnitGuid = unitGuid,
                LocationNo = location.LocationNo,
                Name = location.Name,
                Area = location.Area,
                Building = location.Building,
                Notes = location.Notes != null ? $"Previous storage conditions. Former capacity: {faker.Random.Int(40, 4000)} m²." : null,
                CreatedDate = historicalDate,
                LastModifiedDate = historicalLastModifiedDate
            });
        }
        return historicalLocations;
    }

    // Generer komponenter med realistisk kodehierarki
    static List<Component> GenerateComponents(int count, List<Component> baseComponents, Guid unitGuid, Faker faker, List<Guid> categoryIds)
    {
        var components = new List<Component>();
        DateTime minDate = new DateTime(1753, 1, 1);
        DateTime maxDate = DateTime.Now;

        // Hvis der er baseComponents, generer komponenter baseret på dem
        if (baseComponents != null && baseComponents.Count > 0)
        {
            // Implementer historisk generering som vi vil gøre senere i GenerateHistoricalComponents
            return components;
        }

        // Definer hovedsystemer for komponenter
        var mainSystems = new Dictionary<string, string>
     {
         {"10", "Propulsion System"},
         {"20", "Electrical System"},
         {"30", "Hydraulic System"},
         {"40", "Pneumatic System"},
         {"50", "HVAC System"},
         {"60", "Safety System"},
         {"70", "Cargo Handling System"},
         {"80", "Auxiliary Systems"},
         {"90", "Structural Systems"}
     };

        // Fordel komponenter på forskellige niveauer
        int level1Count = Math.Min((int)(count * 0.05), mainSystems.Count); // 5% på niveau 1
        int level2Count = (int)(count * 0.15); // 15% på niveau 2
        int level3Count = (int)(count * 0.35); // 35% på niveau 3
        int level4Count = (int)(count * 0.30); // 30% på niveau 4
        int level5Count = count - level1Count - level2Count - level3Count - level4Count; // Resten på niveau 5

        // Level 1 komponenter (topniveau)
        var level1Components = new List<Component>();
        var systemCodes = mainSystems.Keys.ToList();
        for (int i = 0; i < level1Count; i++)
        {
            string code = systemCodes[i]; // Brug systematiske koder for niveau 1

            var component = new Component
            {
                Id = Guid.NewGuid(),
                master_id = null,
                UnitGuid = unitGuid,
                ComponentNo = $"C{code}",
                Code = code,
                Level = 1,
                ParentComponentGuid = null,
                Name = mainSystems[code],
                Description = $"Main system for {mainSystems[code].ToLower()} related functions and components",
                CreatedDate = GenerateRandomDate(faker, minDate, maxDate), // TILFØJET
                LastModifiedDate = GenerateNullableRandomDate(faker, minDate, maxDate, 0.6f) // TILFØJET
            };

            components.Add(component);
            level1Components.Add(component);
        }

        // Level 2 komponenter (subsystemer)
        var level2Components = new List<Component>();
        for (int i = 0; i < level2Count; i++)
        {
            var parent = faker.PickRandom(level1Components);
            string subCode = String.Format("{0}.{1:D2}", parent.Code, faker.Random.Number(1, 99));
            string subsystemName = GenerateSubsystemName(parent.Name, faker);

            var component = new Component
            {
                Id = Guid.NewGuid(),
                master_id = null,
                UnitGuid = unitGuid,
                ComponentNo = $"C{subCode.Replace(".", "")}",
                Code = subCode,
                Level = 2,
                ParentComponentGuid = parent.Id,
                Name = subsystemName,
                Description = GenerateComponentDescription(subsystemName, parent.Name, faker, 2),
                CreatedDate = GenerateRandomDate(faker, minDate, maxDate), // TILFØJET
                LastModifiedDate = GenerateNullableRandomDate(faker, minDate, maxDate, 0.6f) // TILFØJET
            };

            components.Add(component);
            level2Components.Add(component);
        }

        // Level 3 komponenter
        var level3Components = new List<Component>();
        for (int i = 0; i < level3Count; i++)
        {
            var parent = faker.PickRandom(level2Components);
            string subCode = String.Format("{0}.{1:D2}", parent.Code, faker.Random.Number(1, 99));
            string componentName = GenerateComponentName(parent.Name, faker);

            var component = new Component
            {
                Id = Guid.NewGuid(),
                master_id = null,
                UnitGuid = unitGuid,
                ComponentNo = $"C{subCode.Replace(".", "")}",
                Code = subCode,
                Level = 3,
                ParentComponentGuid = parent.Id,
                Name = componentName,
                Description = GenerateComponentDescription(componentName, parent.Name, faker, 3),
                CreatedDate = GenerateRandomDate(faker, minDate, maxDate), // TILFØJET
                LastModifiedDate = GenerateNullableRandomDate(faker, minDate, maxDate, 0.6f) // TILFØJET
            };

            components.Add(component);
            level3Components.Add(component);
        }

        // Level 4 komponenter
        var level4Components = new List<Component>();
        for (int i = 0; i < level4Count; i++)
        {
            var parent = faker.PickRandom(level3Components);
            string subCode = String.Format("{0}.{1:D2}", parent.Code, faker.Random.Number(1, 99));
            string assemblyName = GenerateAssemblyName(parent.Name, faker);

            var component = new Component
            {
                Id = Guid.NewGuid(),
                master_id = null,
                UnitGuid = unitGuid,
                ComponentNo = $"C{subCode.Replace(".", "")}",
                Code = subCode,
                Level = 4,
                ParentComponentGuid = parent.Id,
                Name = assemblyName,
                Description = GenerateComponentDescription(assemblyName, parent.Name, faker, 4),
                CreatedDate = GenerateRandomDate(faker, minDate, maxDate), // TILFØJET
                LastModifiedDate = GenerateNullableRandomDate(faker, minDate, maxDate, 0.6f) // TILFØJET
            };

            components.Add(component);
            level4Components.Add(component);
        }

        // Level 5 komponenter (enkeltdele)
        for (int i = 0; i < level5Count; i++)
        {
            var parent = faker.PickRandom(level4Components);
            string subCode = String.Format("{0}.{1:D2}", parent.Code, faker.Random.Number(1, 99));
            string partName = GeneratePartName(parent.Name, faker);

            var component = new Component
            {
                Id = Guid.NewGuid(),
                master_id = null,
                UnitGuid = unitGuid,
                ComponentNo = $"C{subCode.Replace(".", "")}",
                Code = subCode,
                Level = 5,
                ParentComponentGuid = parent.Id,
                Name = partName,
                Description = GenerateComponentDescription(partName, parent.Name, faker, 5),
                CreatedDate = GenerateRandomDate(faker, minDate, maxDate), // TILFØJET
                LastModifiedDate = GenerateNullableRandomDate(faker, minDate, maxDate, 0.6f) // TILFØJET
            };

            components.Add(component);
        }

        return components;
    }

    // Generer historiske versioner af komponenter
    static List<Component> GenerateHistoricalComponents(
        int count,
        List<Component> activeComponents,
        Guid unitGuid,
        Faker faker)
    {
        var historicalComponents = new List<Component>();
        DateTime minHistoricalDate = new DateTime(1753, 1, 1).AddDays(30);

        foreach (var activeComponent in activeComponents)
        {
            // Sikrer at CreatedDate er sat (skulle allerede være gjort i GenerateComponents)
            if (activeComponent.CreatedDate == DateTime.MinValue)
            {
                activeComponent.CreatedDate = DateTime.Now.AddDays(-faker.Random.Int(1, 365));
            }
            if (!activeComponent.LastModifiedDate.HasValue)
            {
                activeComponent.LastModifiedDate = faker.Random.Bool(0.5f) ?
                                                     DateTime.Now.AddDays(-faker.Random.Int(0, 90)) : null;
            }

            // Kun lav historik for nogle komponenter (70%)
            if (faker.Random.Bool(0.7f))
            {
                // Generer 1-3 historiske versioner
                int versionCount = faker.Random.Int(1, 3);

                for (int v = 0; v < versionCount && historicalComponents.Count < count; v++)
                {
                    // Beregn historisk dato (ældre end den aktive)
                    DateTime historicalDate = activeComponent.CreatedDate.AddDays(-faker.Random.Int(180, 1095));
                    DateTime? historicalLastModifiedDate = historicalDate.AddDays(faker.Random.Int(1, 30));

                    // Sikkerhedscheck for historiske datoer
                    if (historicalDate < minHistoricalDate)
                    {
                        historicalDate = minHistoricalDate.AddDays(faker.Random.Int(0, 30));
                        historicalLastModifiedDate = historicalDate.AddDays(faker.Random.Int(1, 30));
                    }
                    else if (historicalLastModifiedDate.HasValue && historicalLastModifiedDate.Value < minHistoricalDate)
                    {
                        historicalLastModifiedDate = minHistoricalDate.AddDays(faker.Random.Int(1, 30));
                    }

                    var historicalComponent = new Component
                    {
                        Id = Guid.NewGuid(),
                        master_id = activeComponent.Id,
                        UnitGuid = unitGuid,
                        ComponentNo = $"{activeComponent.ComponentNo}-REV{v + 1}",
                        Code = activeComponent.Code,
                        Level = activeComponent.Level,
                        ParentComponentGuid = activeComponent.ParentComponentGuid,
                        Name = activeComponent.Name,
                        Description = $"Historical version {v + 1} of {activeComponent.Name}. {faker.Lorem.Sentence()}",
                        CreatedDate = historicalDate,
                        LastModifiedDate = historicalLastModifiedDate
                    };

                    historicalComponents.Add(historicalComponent);
                }
            }
        }

        // Håndtering af antal historiske komponenter (trim og tilføj ekstra)
        if (historicalComponents.Count > count)
        {
            historicalComponents = historicalComponents.Take(count).ToList();
        }
        else if (historicalComponents.Count < count)
        {
            int remaining = count - historicalComponents.Count;
            for (int i = 0; i < remaining; i++)
            {
                var randomActiveComponent = faker.PickRandom(activeComponents);
                var additionalHistorical = new Component
                {
                    Id = Guid.NewGuid(),
                    master_id = randomActiveComponent.Id,
                    UnitGuid = unitGuid,
                    ComponentNo = $"{randomActiveComponent.ComponentNo}-HREV{faker.Random.Number(10, 99)}",
                    Code = randomActiveComponent.Code,
                    Level = randomActiveComponent.Level,
                    ParentComponentGuid = randomActiveComponent.ParentComponentGuid,
                    Name = randomActiveComponent.Name,
                    Description = $"Legacy version of {randomActiveComponent.Name}. {faker.Lorem.Paragraph()}",
                    CreatedDate = DateTime.Now.AddDays(-faker.Random.Int(730, 1825)),
                    LastModifiedDate = DateTime.Now.AddDays(-faker.Random.Int(365, 729))
                };
                historicalComponents.Add(additionalHistorical);
            }
        }

        return historicalComponents;
    }


    static string GetManufacturerNameById(List<Guid> manufacturerIds, Guid manufacturerId, Faker faker)
    {
        // Bemærk: I en rigtig implementation ville man slå op i databasen 
        // baseret på ID'et for at hente producentens navn

        // For vores mockup-formål genererer vi et konsistent navn baseret på GUID
        // Dette sikrer at samme ID altid giver samme producentnavn

        // Brug GUID'ets hashcode til at vælge en konsistent producent
        int hashCode = manufacturerId.GetHashCode();

        // Vælg et land baseret på hash
        var countries = ManufacturersByCountry.Keys.ToArray();
        string country = countries[Math.Abs(hashCode) % countries.Length];

        // Vælg en producent fra dette land baseret på hash
        var manufacturers = ManufacturersByCountry[country];
        if (manufacturers.Count > 0)
        {
            return manufacturers[Math.Abs(hashCode) % manufacturers.Count];
        }

        // Fallback hvis der ikke er producenter for det land
        return $"Manufacturer-{Math.Abs(hashCode) % 1000}";
    }

    // Generer komponent-reservedel relationer
    static List<ComponentPart> GenerateComponentParts(
        List<Component> components,
        List<SparePart> spareParts,
        Guid unitGuid,
        Faker faker)
    {
        var componentParts = new List<ComponentPart>();

        // 1. Forbered data: Gruppér reservedele efter nøgleord
        var groupedSpareParts = spareParts
            .Where(p => p.UnitGuid == unitGuid)
            .SelectMany(p => GetRelevantKeywords(p.Name).Select(keyword => new { Keyword = keyword, Part = p }))
            .ToLookup(item => item.Keyword, item => item.Part);

        // Forbered en liste over alle reservedele for denne UnitGuid (til brug for tilfældige valg)
        var allSparePartsForUnit = spareParts.Where(p => p.UnitGuid == unitGuid).ToList();

        DateTime minDate = new DateTime(1753, 1, 1);
        DateTime maxDate = DateTime.Now;

        foreach (var component in components)
        {
            int partCount = DeterminePartCount(component.Level, component.Name, faker);

            var matchingParts = FindMatchingPartsOptimized(
                groupedSpareParts,
                allSparePartsForUnit,
                component.Name,
                unitGuid,
                partCount,
                faker
            );

            foreach (var part in matchingParts)
            {
                componentParts.Add(new ComponentPart
                {
                    Id = Guid.NewGuid(),
                    master_id = null,
                    UnitGuid = unitGuid,
                    ComponentGuid = component.Id,
                    SparePartGuid = part.Id,
                    Quantity = DeterminePartQuantity(part.Name, component.Name, faker),
                    Position = GeneratePartPosition(part.Name, component.Name, faker),
                    Notes = GenerateAssemblyNotes(part.Name, component.Name, faker),
                    CreatedDate = GenerateRandomDate(faker, minDate, maxDate) // Sæt CreatedDate
                                                                              // LastModifiedDate kan forblive null, hvis det er tilladt i din database
                });
            }
        }

        return componentParts;
    }


    // Generate historical Component-Part relations
    static List<ComponentPart> GenerateHistoricalComponentParts(
        List<ComponentPart> activeComponentParts,
        Guid unitGuid,
        Faker faker)
    {
        var historicalComponentParts = new List<ComponentPart>();

        // Tilføj datoer til alle aktive ComponentPart relationer
        foreach (var activeCP in activeComponentParts)
        {
            activeCP.CreatedDate = DateTime.Now.AddDays(-faker.Random.Int(1, 365));
            activeCP.LastModifiedDate = faker.Random.Bool(0.5f) ?
                DateTime.Now.AddDays(-faker.Random.Int(0, 90)) : null;
        }

        // Vælg tilfældige aktive relationer til at få historik (f.eks. 10%)
        int historyCount = activeComponentParts.Count / 10;
        var selectedRelations = faker.Random.ListItems(activeComponentParts.ToList(), historyCount);

        foreach (var relation in selectedRelations)
        {
            // Generer 1 historisk version
            DateTime historicalDate = relation.CreatedDate.AddDays(-faker.Random.Int(180, 1095));

            var historicalRelation = new ComponentPart
            {
                Id = Guid.NewGuid(),
                master_id = relation.Id,
                UnitGuid = unitGuid,
                ComponentGuid = relation.ComponentGuid,
                SparePartGuid = relation.SparePartGuid,
                // Typiske ændringer vil være i antal og notes
                Quantity = Math.Max(1, relation.Quantity + faker.Random.Int(-2, 2)),
                Position = relation.Position, // Samme position
                Notes = relation.Notes != null ?
                    $"Previous assembly specification. Former torque: {faker.Random.Int(10, 150)} Nm." : null,

                CreatedDate = historicalDate,
                LastModifiedDate = historicalDate.AddDays(faker.Random.Int(1, 30))
            };

            historicalComponentParts.Add(historicalRelation);
        }

        return historicalComponentParts;
    }

    // Hjælpemetoder til realistisk datagenerering

    static string GenerateSupplierContactInfo(Faker faker)
    {
        // Skab realistisk kontaktinfo
        return $"{faker.Name.FullName()}, {faker.Phone.PhoneNumber()}, {faker.Internet.Email()}";
    }

    static string GenerateSupplierNotes(Faker faker, string supplierType)
    {
        var notes = new List<string>();

        if (supplierType.Contains("Manufacturer"))
        {
            notes.Add("Direct manufacturer with complete product range.");
            notes.Add($"Warranty: {faker.Random.Int(12, 36)} months on all supplied parts.");
        }
        else if (supplierType.Contains("Distributor"))
        {
            notes.Add("Authorized distributor with certified genuine parts.");
            notes.Add($"Lead time: {faker.Random.Int(2, 14)} days for standard items.");
        }
        else if (supplierType.Contains("Specialized"))
        {
            notes.Add("Specialized in low-volume, high-precision components.");
            notes.Add($"Technical support available for complex applications.");
        }
        else
        {
            notes.Add($"Established supplier since {faker.Random.Int(1980, 2020)}.");
            notes.Add($"Typical delivery: {faker.Random.Int(1, 10)} working days.");
        }

        // Tilføj kommentar om pris eller kvalitet
        if (faker.Random.Bool())
        {
            notes.Add($"Pricing: {faker.PickRandom(new[] { "Competitive", "Premium", "High-end", "Value-oriented", "Variable" })}");
        }
        else
        {
            notes.Add($"Quality rating: {faker.Random.Int(3, 5)}/5 based on past deliveries.");
        }

        return string.Join(" ", notes);
    }

    static string GenerateManufacturerNotes(Faker faker)
    {
        var notes = new List<string>();

        // Kvalitets- og standardinformation
        if (faker.Random.Bool(0.7f))
        {
            notes.Add($"ISO {faker.Random.Int(9001, 9004)}:{faker.Random.Int(2015, 2020)} certified manufacturer.");
        }

        // Specialisering
        notes.Add($"Specializes in {faker.PickRandom(new[] {
            "high-pressure systems", "marine applications", "hazardous areas",
            "heavy-duty equipment", "corrosion-resistant solutions",
            "high-temperature environments", "precision components",
            "low-maintenance systems", "energy-efficient solutions"
        })}.");

        // Service og support
        if (faker.Random.Bool(0.6f))
        {
            notes.Add($"Technical support available in {faker.Random.Int(3, 15)} languages.");
        }

        return string.Join(" ", notes);
    }

    static string GenerateLocationNotes(Faker faker)
    {
        var notes = new List<string>();

        // Adgangsinfo
        notes.Add($"Access level: {faker.PickRandom(new[] { "General", "Restricted", "Authorized Personnel", "Technical Staff" })}.");

        // Lager- eller placeringsdetaljer
        if (faker.Random.Bool(0.7f))
        {
            notes.Add($"Storage capacity: {faker.Random.Int(50, 5000)} m² with {faker.Random.Int(2, 20)} loading docks.");
        }
        else
        {
            notes.Add($"Equipment area with {faker.Random.Int(2, 50)} service access points.");
        }

        // Sikkerhedsinfo
        if (faker.Random.Bool(0.3f))
        {
            notes.Add($"Special conditions: {faker.PickRandom(new[] {
                "Temperature controlled (18-22°C)",
                "Humidity controlled (40-60%)",
                "ESD protected",
                "Hazardous materials storage",
                "Clean room environment",
                "Vibration isolated"
            })}.");
        }

        return string.Join(" ", notes);
    }

    static string GenerateSubsystemName(string parentSystemName, Faker faker)
    {
        // Udled basissystemet
        string baseSystem = parentSystemName.Split(' ')[0];

        // Generer passende subsystem baseret på hovedsystemtypen
        if (parentSystemName.Contains("Propulsion"))
        {
            return $"{baseSystem} {faker.PickRandom(new[] {
                "Thruster", "Main Engine", "Gearbox", "Shaft Line", "Propeller",
                "Maneuvering", "Steering", "Control System", "Fuel System", "Cooling"
            })}";
        }
        else if (parentSystemName.Contains("Electrical"))
        {
            return $"{baseSystem} {faker.PickRandom(new[] {
                "Power Distribution", "Control Panel", "Generator", "Transformer", "Battery",
                "UPS", "Switchboard", "Cable System", "Lighting", "Emergency Power"
            })}";
        }
        else if (parentSystemName.Contains("Hydraulic"))
        {
            return $"{baseSystem} {faker.PickRandom(new[] {
                "Pump Station", "Cylinder System", "Valve Control", "Accumulator", "Tank",
                "Filter System", "Cooling Unit", "Pressure Control", "Hose System", "Manifold"
            })}";
        }
        else if (parentSystemName.Contains("Pneumatic"))
        {
            return $"{baseSystem} {faker.PickRandom(new[] {
                "Compressor", "Air Dryer", "Valve Station", "Filter Unit", "Cylinder Group",
                "Pressure Control", "Distribution", "Regulator Station", "Lubrication", "Instrumentation"
            })}";
        }
        else if (parentSystemName.Contains("HVAC"))
        {
            return $"{baseSystem} {faker.PickRandom(new[] {
                "Cooling Unit", "Heating System", "Ventilation", "Air Handling", "Duct System",
                "Filtration", "Humidity Control", "Temperature Regulation", "Fan System", "Damper Control"
            })}";
        }
        else if (parentSystemName.Contains("Safety"))
        {
            return $"{baseSystem} {faker.PickRandom(new[] {
                "Fire Detection", "Extinguishing", "Emergency Stop", "Gas Detection", "Evacuation",
                "Alarm System", "Rescue Equipment", "Emergency Lighting", "Life Support", "First Aid"
            })}";
        }
        else
        {
            return $"{baseSystem} {faker.Commerce.ProductAdjective()} {faker.PickRandom(new[] {
                "System", "Unit", "Assembly", "Module", "Section", "Group", "Component"
            })}";
        }
    }

    static string GenerateComponentName(string parentName, Faker faker)
    {
        // Udled basiskomponenten
        string[] parts = parentName.Split(' ');
        string baseComponent = parts[parts.Length - 1];

        if (parentName.Contains("Pump"))
        {
            return $"{faker.PickRandom(new[] {
                "Centrifugal", "Positive Displacement", "Screw", "Gear", "Vane", "Rotary Lobe",
                "Diaphragm", "Peristaltic", "Piston", "Progressive Cavity", "Submersible", "Vertical"
            })} {baseComponent}";
        }
        else if (parentName.Contains("Valve"))
        {
            return $"{faker.PickRandom(new[] {
                "Ball", "Gate", "Globe", "Butterfly", "Check", "Needle", "Diaphragm", "Pinch",
                "Pressure Relief", "Control", "Solenoid", "Pneumatic Actuated", "Hydraulic Actuated"
            })} {baseComponent}";
        }
        else if (parentName.Contains("Motor"))
        {
            return $"{faker.PickRandom(new[] {
                "Induction", "Synchronous", "DC", "Servo", "Stepper", "Brushless DC", "Variable Frequency",
                "High-Torque", "Explosion-Proof", "Inverter Duty", "Marine Duty", "TEFC"
            })} {baseComponent}";
        }
        else if (parentName.Contains("Cylinder"))
        {
            return $"{faker.PickRandom(new[] {
                "Single-Acting", "Double-Acting", "Telescopic", "Tandem", "Rotary", "Tie-Rod",
                "Welded", "Position Sensing", "Cushioned", "Heavy-Duty", "Compact", "ISO Standard"
            })} {baseComponent}";
        }
        else if (parentName.Contains("Sensor") || parentName.Contains("Transmitter"))
        {
            return $"{faker.PickRandom(new[] {
                "Pressure", "Temperature", "Flow", "Level", "Proximity", "Vibration", "Position",
                "Speed", "pH", "Conductivity", "Turbidity", "Optical", "Ultrasonic", "Capacitive"
            })} {baseComponent}";
        }
        else
        {
            return $"{faker.Commerce.ProductAdjective()} {baseComponent}";
        }
    }

    static string GenerateAssemblyName(string parentName, Faker faker)
    {
        // Skab assemblynavn baseret på forældrekomponenten
        string prefix = "";

        if (parentName.Contains("Pump"))
        {
            prefix = faker.PickRandom(new[] {
                "Impeller", "Shaft", "Seal", "Bearing", "Housing", "Cover", "Base Plate",
                "Coupling", "Drive Assembly", "Inlet", "Outlet", "Cooling Jacket"
            });
        }
        else if (parentName.Contains("Valve"))
        {
            prefix = faker.PickRandom(new[] {
                "Body", "Bonnet", "Stem", "Seat", "Disc", "Actuator", "Gearbox",
                "Positioner", "Limit Switch", "Indicator", "Handwheel", "Spring Return"
            });
        }
        else if (parentName.Contains("Motor"))
        {
            prefix = faker.PickRandom(new[] {
                "Stator", "Rotor", "Bearing", "End Shield", "Terminal Box", "Cooling Fan",
                "Encoder", "Brake", "Shaft", "Housing", "Winding", "Brush Holder"
            });
        }
        else if (parentName.Contains("Electrical"))
        {
            prefix = faker.PickRandom(new[] {
                "Circuit Breaker", "Contactor", "Relay", "Terminal", "Power Supply", "Transformer",
                "Bus Bar", "Disconnector", "Switch", "Indicator Lamp", "Overload Protection"
            });
        }
        else
        {
            prefix = faker.PickRandom(new[] {
                "Housing", "Frame", "Cover", "Mounting", "Support", "Guide",
                "Connection", "Adapter", "Insert", "Control Unit", "Adjustment", "Holder"
            });
        }

        return $"{prefix} Assembly";
    }

    static string GeneratePartName(string parentName, Faker faker)
    {
        // Generer et realistisk reservedelsnavn baseret på assembly-type
        var prefixes = new List<string>();

        if (parentName.Contains("Seal") || parentName.Contains("Gasket"))
        {
            prefixes.AddRange(new[] {
                    "O-Ring", "V-Ring", "Lip Seal", "Radial Seal", "Mechanical Seal",
                    "Face Seal", "Gasket", "Packing Ring", "Wiper Seal", "Backup Ring"
                });
        }
        else if (parentName.Contains("Bearing"))
        {
            prefixes.AddRange(new[] {
                    "Ball Bearing", "Roller Bearing", "Needle Bearing", "Tapered Bearing",
                    "Thrust Bearing", "Angular Contact Bearing", "Journal Bearing", "Sleeve Bearing"
                });
        }
        else if (parentName.Contains("Cover") || parentName.Contains("Housing"))
        {
            prefixes.AddRange(new[] {
                    "Gasket", "Bolt", "Nut", "Washer", "Stud", "Plate", "Bracket", "Shield",
                    "Fastener", "Clip", "Clamp", "Pin"
                });
        }
        else if (parentName.Contains("Electrical") || parentName.Contains("Control"))
        {
            prefixes.AddRange(new[] {
                    "Sensor", "Switch", "Relay", "Contactor", "Fuse", "Terminal", "Connector",
                    "Cable", "Wire", "PCB", "Resistor", "Capacitor"
                });
        }
        else
        {
            prefixes.AddRange(new[] {
                    "Bolt", "Nut", "Screw", "Washer", "Pin", "Key", "Spring", "Gear",
                    "Bushing", "Spacer", "Shim", "Clip", "Fastener"
                });
        }

        string prefix = faker.PickRandom(prefixes);

        return $"{prefix} {faker.Commerce.ProductAdjective()}";
    }

    static string GenerateComponentDescription(string componentName, string parentName, Faker faker, int level)
    {
        var descriptions = new List<string>();

        // Basissætning baseret på niveau
        switch (level)
        {
            case 1:
                descriptions.Add($"Main system for {componentName.ToLower()} related operations.");
                break;
            case 2:
                descriptions.Add($"Subsystem handling {componentName.ToLower()} functions within the {parentName.ToLower()} system.");
                break;
            case 3:
                descriptions.Add($"Component for {componentName.ToLower()} operations within the {parentName.ToLower()}.");
                break;
            case 4:
                descriptions.Add($"Assembly of parts comprising the {componentName.ToLower()} within the {parentName.ToLower()}.");
                break;
            case 5:
                descriptions.Add($"Individual part used in the {parentName.ToLower()}.");
                break;
        }

        // Tekniske specifikationer baseret på komponenttype
        if (componentName.Contains("Hydraulic") || componentName.Contains("Pneumatic"))
        {
            descriptions.Add($"Operating pressure: {faker.Random.Int(10, 350)} bar. Temperature range: {faker.Random.Int(-20, 20)} to {faker.Random.Int(80, 120)}°C.");
        }
        else if (componentName.Contains("Electric") || componentName.Contains("Motor"))
        {
            descriptions.Add($"Power rating: {faker.Random.Int(5, 500)} kW. Voltage: {faker.PickRandom(new[] { "24V DC", "230V AC", "400V AC", "690V AC" })}. Protection class: IP{faker.Random.Int(54, 67)}.");
        }
        else if (componentName.Contains("Pump") || componentName.Contains("Compressor"))
        {
            descriptions.Add($"Capacity: {faker.Random.Int(10, 1000)} {faker.PickRandom(new[] { "m³/h", "l/min", "gpm" })}. Head: {faker.Random.Int(10, 200)} {faker.PickRandom(new[] { "m", "ft", "bar" })}.");
        }

        // Vedligeholdelsesinfo på niveau 3-5
        if (level >= 3 && faker.Random.Bool(0.6f))
        {
            descriptions.Add($"Maintenance interval: {faker.Random.Int(3, 36)} months under normal operation.");
        }

        // Materialeinformation på niveau 4-5
        if (level >= 4 && faker.Random.Bool(0.7f))
        {
            descriptions.Add($"Material: {GetComponentMaterial(componentName, faker)}.");
        }

        return string.Join(" ", descriptions);
    }

    static string GetComponentMaterial(string componentName, Faker faker)
    {
        // Vælg et passende materiale baseret på komponenttype
        foreach (var entry in MaterialsByComponent)
        {
            if (componentName.Contains(entry.Key))
            {
                return faker.PickRandom(entry.Value);
            }
        }

        // Default materialer hvis ingen specifik match
        return faker.PickRandom(new[] {
                "Carbon Steel", "Stainless Steel 304", "Stainless Steel 316", "Cast Iron",
                "Aluminum", "Brass", "Bronze", "Plastic", "Rubber", "PTFE"
            });
    }

    static string GenerateRealisticPartName(string partType, string partFamily, Faker faker)
    {
        // Skab realistiske navne med industristandarder og varianter
        string prefix = faker.PickRandom(new[] {
                faker.Commerce.ProductAdjective(),
                GetStandardSpec(partType, faker),
                GetMaterialPrefix(partType, faker),
                GetSizePrefix(partType, faker)
            });

        return $"{prefix} {partType}{partFamily}";
    }

    static string GetStandardSpec(string partType, Faker faker)
    {
        // Industristandarder baseret på deltype
        if (partType.Contains("Valve"))
        {
            return faker.PickRandom(new[] { "DIN", "ANSI", "API", "JIS", "BS" });
        }
        else if (partType.Contains("Pump"))
        {
            return faker.PickRandom(new[] { "API 610", "ANSI B73.1", "ISO 5199", "DIN 24256" });
        }
        else if (partType.Contains("Motor"))
        {
            return faker.PickRandom(new[] { "IEC", "NEMA", "IP66", "Ex d", "ATEX" });
        }
        else if (partType.Contains("Bearing"))
        {
            return faker.PickRandom(new[] { "ISO", "DIN", "SKF", "NSK", "FAG", "NTN" });
        }
        else if (partType.Contains("Seal"))
        {
            return faker.PickRandom(new[] { "ISO", "DIN", "EN", "API", "Cartridge" });
        }
        else
        {
            return faker.PickRandom(new[] { "DIN", "ISO", "ANSI", "JIS", "BS", "API" });
        }
    }

    static string GetMaterialPrefix(string partType, Faker faker)
    {
        // Materiale-præfiks baseret på deltype
        foreach (var entry in MaterialsByComponent)
        {
            if (partType.Contains(entry.Key))
            {
                // Vælg et kort materialenavn der egner sig som præfiks
                var materials = entry.Value
                    .Where(m => m.Length < 15 && !m.Contains(" "))
                    .ToList();

                if (materials.Count > 0)
                {
                    return faker.PickRandom(materials);
                }

                // Hvis alle materialer har mellemrum, returner første ord
                if (entry.Value.Count > 0)
                {
                    string material = faker.PickRandom(entry.Value);
                    return material.Split(' ')[0];
                }
            }
        }

        // Default materialer
        return faker.PickRandom(new[] {
                "Steel", "SS316", "Bronze", "Brass", "Plastic", "Rubber",
                "PTFE", "Aluminum", "Titanium", "Ceramic"
            });
    }

    static string GetSizePrefix(string partType, Faker faker)
    {
        // Størrelsespræfiks baseret på deltype
        if (partType.Contains("Valve") || partType.Contains("Pipe") || partType.Contains("Fitting"))
        {
            return faker.PickRandom(new[] {
                    "DN15", "DN25", "DN40", "DN50", "DN80", "DN100", "DN150", "DN200",
                    "1/2\"", "1\"", "1-1/2\"", "2\"", "3\"", "4\"", "6\"", "8\""
                });
        }
        else if (partType.Contains("Bolt") || partType.Contains("Screw") || partType.Contains("Nut"))
        {
            return faker.PickRandom(new[] {
                    "M6", "M8", "M10", "M12", "M16", "M20", "M24", "M30",
                    "1/4\"", "3/8\"", "1/2\"", "5/8\"", "3/4\"", "1\""
                });
        }
        else if (partType.Contains("Pump") || partType.Contains("Motor"))
        {
            return faker.PickRandom(new[] {
                    "1.5kW", "2.2kW", "4kW", "7.5kW", "11kW", "15kW", "22kW", "30kW", "45kW", "75kW"
                });
        }
        else if (partType.Contains("Bearing"))
        {
            return faker.PickRandom(new[] {
                    "6204", "6205", "6206", "6207", "6208", "6305", "6306", "6307", "22216", "22217"
                });
        }
        else
        {
            return faker.PickRandom(new[] {
                    "Type-A", "Type-B", "Series-1", "Series-2", "Std", "XL", "HS", "HP"
                });
        }
    }

    static string GeneratePartNumber(string partType, Faker faker)
    {
        // Generer varenummer der afspejler industristandarder og leverandørkoder
        var formats = new Func<string>[] {
                // Format: ABC-12345
                () => $"{faker.Random.String2(3, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")}-{faker.Random.Number(10000, 99999)}",
                
                // Format: 123-456-789
                () => $"{faker.Random.Number(100, 999)}-{faker.Random.Number(100, 999)}-{faker.Random.Number(100, 999)}",
                
                // Format: ABC/12-34/567
                () => $"{faker.Random.String2(3, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")}/{faker.Random.Number(10, 99)}-{faker.Random.Number(10, 99)}/{faker.Random.Number(100, 999)}",
                
                // Format: P-1234567
                () => $"P-{faker.Random.Number(1000000, 9999999)}",
                
                // Format: [Type Code][Number]-[Variant]
                () => $"{GetPartTypeCode(partType)}{faker.Random.Number(1000, 9999)}-{faker.Random.String2(1, "ABCDEFGHJK")}"
            };

        return faker.PickRandom(formats)();
    }

    static string GetPartTypeCode(string partType)
    {
        // Kode baseret på deltype
        if (partType.Contains("Valve")) return "V";
        if (partType.Contains("Pump")) return "P";
        if (partType.Contains("Motor")) return "M";
        if (partType.Contains("Bearing")) return "B";
        if (partType.Contains("Seal")) return "S";
        if (partType.Contains("Gasket")) return "G";
        if (partType.Contains("Filter")) return "F";
        if (partType.Contains("Sensor")) return "SE";
        if (partType.Contains("Switch")) return "SW";
        if (partType.Contains("Bolt") || partType.Contains("Screw") || partType.Contains("Nut")) return "FA";

        // Default
        return "C";
    }

    static string GenerateRevisionedPartNumber(string basePartNumber, Faker faker)
    {
        // Skab en revisioneret version af et eksisterende varenummer
        var revisionFormats = new Func<string, string>[] {
                // Format: Original-R1
                (original) => $"{original}-R{faker.Random.Number(1, 9)}",
                
                // Format: Original.1
                (original) => $"{original}.{faker.Random.Number(1, 9)}",
                
                // Format: Original/A
                (original) => $"{original}/{faker.Random.String2(1, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")}",
                
                // Format: Original-MOD
                (original) => $"{original}-MOD",
                
                // Format: Original_Rev1
                (original) => $"{original}_Rev{faker.Random.Number(1, 9)}"
            };

        return faker.PickRandom(revisionFormats)(basePartNumber);
    }

    static string GeneratePartSerialCode(string partType, string manufacturer, Faker faker)
    {
        // Genererer seriekoder baseret på producent og deltype - forskellige formater for forskellige producenter
        if (manufacturer.Contains("Siemens"))
        {
            // Siemens format: S-[år][måned]-[6 cifre]
            return $"S-{faker.Random.Int(18, 25)}{faker.Random.Int(1, 12):D2}-{faker.Random.Number(100000, 999999)}";
        }
        else if (manufacturer.Contains("ABB"))
        {
            // ABB format: [3 bogstaver]-[7 cifre]
            return $"{faker.Random.String2(3, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")}-{faker.Random.Number(1000000, 9999999)}";
        }
        else if (manufacturer.Contains("Grundfos"))
        {
            // Grundfos format: [type]-[år][uge]-[5 cifre]
            return $"{GetPartTypeCode(partType)}-{faker.Random.Int(18, 25)}{faker.Random.Int(1, 52):D2}-{faker.Random.Number(10000, 99999)}";
        }
        else if (manufacturer.Contains("Danfoss"))
        {
            // Danfoss format: D[cifre]/[år][2 bogstaver]
            return $"D{faker.Random.Number(10000, 99999)}/{faker.Random.Int(18, 25)}{faker.Random.String2(2, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")}";
        }
        else if (manufacturer.Contains("Bosch"))
        {
            // Bosch format: [4 cifre] [3 cifre] [3 cifre]
            return $"{faker.Random.Number(1000, 9999)} {faker.Random.Number(100, 999)} {faker.Random.Number(100, 999)}";
        }
        else
        {
            // Generisk format: [3 bogstaver][2 cifre]-[4 cifre]-[3 cifre]
            return $"{faker.Random.String2(3, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")}{faker.Random.Number(10, 99)}-" +
                   $"{faker.Random.Number(1000, 9999)}-{faker.Random.Number(100, 999)}";
        }
    }

    static string GenerateTypeNumber(string partType, Faker faker)
    {
        // Typekode der afspejler produktets klassifikation
        var formats = new Func<string>[] {
                // Format: T-1234
                () => $"T-{faker.Random.Number(1000, 9999)}",
                
                // Format: [Type Code]123
                () => $"{GetPartTypeCode(partType)}{faker.Random.Number(100, 999)}",
                
                // Format: TY-A12
                () => $"TY-{faker.Random.String2(1, "ABCDEFGHIJKLMNOPQRSTUVWXYZ")}{faker.Random.Number(10, 99)}",
                
                // Format: TYPE/123/45
                () => $"TYPE/{faker.Random.Number(100, 999)}/{faker.Random.Number(10, 99)}"
            };

        return faker.PickRandom(formats)();
    }

    static string GeneratePartDescription(string name, string partType, Faker faker)
    {
        var descriptions = new List<string>();

        // Start med en basisforklaring
        descriptions.Add($"{name} for industrial applications in {faker.PickRandom(new[] {
                "critical", "standard", "heavy-duty", "marine", "offshore", "process", "chemical", "food grade"
            })} environments.");

        // Tilføj materialebeskrivelse
        string material = GetComponentMaterial(partType, faker);
        if (!String.IsNullOrEmpty(material))
        {
            descriptions.Add($"Made from {material.ToLower()}.");
        }

        // Tilføj tekniske specifikationer baseret på deltype
        if (partType.Contains("Valve"))
        {
            descriptions.Add($"Pressure rating: PN{faker.Random.Int(16, 40)}/{faker.Random.Int(150, 400)} {faker.PickRandom(new[] { "bar", "psi" })}. Temperature range: {faker.Random.Int(-50, 0)} to {faker.Random.Int(100, 450)}°C.");
        }
        else if (partType.Contains("Pump"))
        {
            descriptions.Add($"Flow rate: {faker.Random.Int(1, 1000)} {faker.PickRandom(new[] { "m³/h", "l/min", "gpm" })}. Head: {faker.Random.Int(10, 500)} {faker.PickRandom(new[] { "m", "bar", "psi" })}. Efficiency: {faker.Random.Int(50, 95)}%.");
        }
        else if (partType.Contains("Motor"))
        {
            descriptions.Add($"Power: {faker.Random.Int(1, 500)} kW. Speed: {faker.Random.Int(750, 3600)} rpm. Voltage: {faker.PickRandom(new[] { "230V", "400V", "690V", "3.3kV", "6.6kV" })}. Frequency: {faker.PickRandom(new[] { "50Hz", "60Hz" })}.");
        }
        else if (partType.Contains("Bearing"))
        {
            descriptions.Add($"Dynamic load rating: {faker.Random.Int(10, 1000)} kN. Static load rating: {faker.Random.Int(5, 800)} kN. Speed rating: {faker.Random.Int(1000, 10000)} rpm.");
        }
        else if (partType.Contains("Seal") || partType.Contains("Gasket"))
        {
            descriptions.Add($"Temperature resistance: {faker.Random.Int(-50, 0)} to {faker.Random.Int(100, 450)}°C. Pressure rating: {faker.Random.Int(10, 400)} {faker.PickRandom(new[] { "bar", "psi" })}. Chemical resistance: {faker.PickRandom(new[] { "High", "Medium", "Standard" })}.");
        }

        // Tilføj kompatibilitetsinformation
        if (faker.Random.Bool(0.7f))
        {
            descriptions.Add($"Compatible with {faker.PickRandom(new[] {
                    "standard industrial", "marine-grade", "offshore", "chemical processing",
                    "food industry", "pharmaceutical", "high-temperature", "cryogenic",
                    "ATEX-rated", "hazardous area", "Zone 1", "Zone 2", "clean room"
                })} applications.");
        }

        // Tilføj certificering hvis relevant
        if (faker.Random.Bool(0.3f))
        {
            descriptions.Add($"Certified to {faker.PickRandom(new[] {
                    "ISO 9001", "ISO 14001", "API 6D", "API 607", "PED 2014/68/EU",
                    "ATEX 2014/34/EU", "IECEx", "NSF", "3-A", "DNV-GL", "ABS", "Lloyd's Register"
                })} standards.");
        }

        return string.Join(" ", descriptions);
    }

    static string GenerateVariantDescription(string baseDescription, Faker faker)
    {
        // Skaber en variant af en basisbeskrivelse
        var newDescriptionParts = baseDescription.Split('.').ToList();

        // Erstat en sætning med noget nyt hvis der er mere end én sætning
        if (newDescriptionParts.Count > 1)
        {
            int indexToReplace = faker.Random.Int(0, newDescriptionParts.Count - 1);
            newDescriptionParts[indexToReplace] = $" {faker.PickRandom(new[] {
                    "Previous version with different specifications",
                    "Earlier model with legacy components",
                    "Superseded design variant",
                    "Historical product version",
                    "Replaced by newer model",
                    "Alternative configuration",
                    "Modified specifications compared to current model"
                })}";
        }
        else
        {
            // Bare tilføj en note om at det er en historisk variant
            newDescriptionParts.Add($" Historical variant from previous design iteration");
        }

        return string.Join(".", newDescriptionParts);
    }

    static string GeneratePartNotes(string name, string partType, Faker faker)
    {
        var notes = new List<string>();

        // Vedligeholdelsesnoter
        if (faker.Random.Bool(0.5f))
        {
            notes.Add($"Maintenance requirement: {faker.PickRandom(new[] {
                    "Inspect every 6 months",
                    "Annual replacement recommended",
                    "Visual inspection at 3-month intervals",
                    "No routine maintenance required",
                    "Lubricate every 1000 operating hours",
                    "Clean and inspect quarterly",
                    "Replace seals every 2 years"
                })}.");
        }

        // Lageroplysninger
        if (faker.Random.Bool(0.4f))
        {
            notes.Add($"Inventory note: {faker.PickRandom(new[] {
                    "Critical spare; keep minimum 2 in stock",
                    "Long lead time item (6-8 weeks)",
                    "Standard stock item",
                    "Order on demand only",
                    "Manufacturer recommends keeping a backup unit",
                    "Fast-moving component, monitor levels",
                    "Emergency replacement available from local supplier"
                })}.");
        }

        // Installationsnoter
        if (faker.Random.Bool(0.3f))
        {
            notes.Add($"Installation note: {faker.PickRandom(new[] {
                    "Torque to manufacturer specifications",
                    "Use proper gasket sealant",
                    "Requires special installation tool",
                    "Align marks during assembly",
                    "Check clearance after installation",
                    "Match rotation direction arrows",
                    "Do not over-tighten"
                })}.");
        }

        // Kompatibilitetsnoter
        if (faker.Random.Bool(0.3f))
        {
            notes.Add($"Compatibility: {faker.PickRandom(new[] {
                    "Also fits older model series",
                    "Compatible with previous generation",
                    "Not interchangeable with variant model",
                    "Direct replacement for discontinued part",
                    "Can substitute for multiple part numbers",
                    "OEM equivalent to aftermarket part",
                    "Verify dimensions before use"
                })}.");
        }

        // Sæt nogle søgeord, så vi kan teste søgefunktionaliteten
        if (faker.Random.Bool(0.2f))
        {
            string searchTerms = string.Join(", ",
                faker.Random.ListItems(CommonSearchTerms, faker.Random.Int(2, 5))
            );
            notes.Add($"Keywords: {searchTerms}");
        }

        return string.Join(" ", notes);
    }

    static string GenerateHistoricalPartNotes(string partName, Faker faker)
    {
        var notes = new List<string>();

        // Forældelsesoplysninger
        notes.Add($"{faker.PickRandom(new[] {
                "Superseded by newer version",
                "Replaced in current design",
                "Historical version of current part",
                "No longer active in new installations",
                "Legacy component from earlier version",
                "Discontinued by manufacturer",
                "Design revision from previous generation"
            })}.");

        // Kompatibilitetsadvarsel
        if (faker.Random.Bool(0.4f))
        {
            notes.Add($"{faker.PickRandom(new[] {
                    "Not compatible with latest revision",
                    "Check specifications before substitution",
                    "May require adaptation for current system",
                    "Some modifications necessary for current use",
                    "Dimensions differ from current standard",
                    "Performance characteristics differ from current version",
                    "Use as direct replacement not recommended"
                })}.");
        }

        // Årsag til ændring
        if (faker.Random.Bool(0.6f))
        {
            notes.Add($"Reason for revision: {faker.PickRandom(new[] {
                    "Improved reliability",
                    "Enhanced performance",
                    "Material upgrade",
                    "Manufacturing optimization",
                    "Cost reduction",
                    "Compliance with new standards",
                    "Extended service life",
                    "Better compatibility",
                    "Integration of feedback from field"
                })}.");
        }

        return string.Join(" ", notes);
    }

    static string GenerateManufacturerNameById(List<Guid> manufacturerIds, Guid manufacturerId, Faker faker)
    {
        // I en rigtig implementation ville man slå producenten op i databasen
        // Her returnerer vi blot et simuleret producentnavn

        foreach (var country in ManufacturersByCountry.Keys)
        {
            if (ManufacturersByCountry[country].Count > 0)
            {
                return faker.PickRandom(ManufacturersByCountry[country]);
            }
        }

        return "Generic Manufacturer";
    }

    static string GetBasePartType(string partType)
    {
        // Udled basistypen af en del
        if (partType.Contains("Valve")) return "Valves";
        if (partType.Contains("Pump")) return "Pumps";
        if (partType.Contains("Motor")) return "Motors";
        if (partType.Contains("Bearing")) return "Bearings";
        if (partType.Contains("Seal")) return "Seals";

        return "Other";
    }

    static string GetPartTypeFromName(string partName)
    {
        // Udled deltype fra et navn
        foreach (var category in ComponentsByCategory.Values)
        {
            foreach (var partType in category)
            {
                if (partName.Contains(partType))
                {
                    return partType;
                }
            }
        }

        return "Generic Part";
    }

    static Guid GetAppropriateCategory(List<Guid> categoryIds, string partType, Faker faker)
    {
        // I en rigtig implementation ville man vælge en passende kategori baseret på deltype
        // Her returnerer vi blot et tilfældigt kategoris-ID
        return faker.PickRandom(categoryIds);
    }

    static List<SparePart> FindMatchingParts(List<SparePart> spareParts, string componentName, Guid unitGuid, int count, Faker faker)
    {
        // Find reservedele der passer til komponenten baseret på navn
        var componentBaseName = componentName.Split(' ')[0];

        // Prøv først at finde dele der direkte matcher komponentens type
        var matchingParts = spareParts
            .Where(p => p.UnitGuid == unitGuid &&
                  (p.Name.Contains(componentBaseName) ||
                   DeterminePartTypeRelevance(p.Name, componentName)))
            .ToList();

        // Hvis vi ikke har nok matchende dele, tilføj nogle tilfældige
        if (matchingParts.Count < count)
        {
            var remainingCount = count - matchingParts.Count;
            var randomParts = spareParts
                .Where(p => p.UnitGuid == unitGuid && !matchingParts.Contains(p))
                .OrderBy(_ => Guid.NewGuid())
                .Take(remainingCount)
                .ToList();

            matchingParts.AddRange(randomParts);
        }

        // Begræns til det ønskede antal
        return matchingParts.Take(count).ToList();
    }

    // Hjælpefunktion til at udtrække relevante nøgleord fra et reservedelsnavn
    static List<string> GetRelevantKeywords(string partName)
    {
        return new List<string> { partName.Split(' ')[0], partName, partName.ToLower() }; // Tilføj partName.ToLower()
                                                                                          // Tilføj mere avanceret logik for at udtrække flere relevante nøgleord, hvis nødvendigt
    }

    static List<SparePart> FindMatchingPartsOptimized(
    ILookup<string, SparePart> groupedSpareParts, // Ændret til ILookup
    List<SparePart> allSparePartsForUnit,
    string componentName,
    Guid unitGuid,
    int count,
    Faker faker)
    {
        var matchingParts = new HashSet<SparePart>();
        var componentBaseName = componentName.Split(' ')[0];

        // Tjek direkte match på base name
        if (groupedSpareParts.Contains(componentBaseName))
        {
            foreach (var part in groupedSpareParts[componentBaseName].Where(p => p.UnitGuid == unitGuid))
            {
                matchingParts.Add(part);
                if (matchingParts.Count >= count) return matchingParts.Take(count).ToList();
            }
        }

        // Tjek direkte match på hele component name
        if (groupedSpareParts.Contains(componentName))
        {
            foreach (var part in groupedSpareParts[componentName].Where(p => p.UnitGuid == unitGuid && !matchingParts.Contains(p)))
            {
                matchingParts.Add(part);
                if (matchingParts.Count >= count) return matchingParts.Take(count).ToList();
            }
        }

        // Tjek relevans
        foreach (var part in allSparePartsForUnit.Where(p => !matchingParts.Contains(p) && DeterminePartTypeRelevance(p.Name, componentName)))
        {
            matchingParts.Add(part);
            if (matchingParts.Count >= count) return matchingParts.Take(count).ToList();
        }

        // Tilføj tilfældige
        var remainingCount = count - matchingParts.Count;
        var randomParts = allSparePartsForUnit
            .Where(p => !matchingParts.Contains(p))
            .OrderBy(_ => Guid.NewGuid())
            .Take(remainingCount);

        return matchingParts.Concat(randomParts).Take(count).ToList();
    }

    static bool DeterminePartTypeRelevance(string partName, string componentName)
    {
        // Lav en mere sofistikeret vurdering af om en reservedel er relevant for en komponent

        // Tjek for pumpekomponenter
        if (componentName.Contains("Pump"))
        {
            return partName.Contains("Impeller") ||
                   partName.Contains("Seal") ||
                   partName.Contains("Shaft") ||
                   partName.Contains("Bearing") ||
                   partName.Contains("Gasket") ||
                   partName.Contains("Coupling");
        }

        // Tjek for motorkomponenter
        if (componentName.Contains("Motor"))
        {
            return partName.Contains("Bearing") ||
                                   partName.Contains("Shaft") ||
                                   partName.Contains("Winding") ||
                                   partName.Contains("Brush") ||
                                   partName.Contains("Commutator") ||
                                   partName.Contains("Fan");
        }

        // Tjek for ventilkomponenter
        if (componentName.Contains("Valve"))
        {
            return partName.Contains("Seat") ||
                   partName.Contains("Disc") ||
                   partName.Contains("Stem") ||
                   partName.Contains("Actuator") ||
                   partName.Contains("Seal") ||
                   partName.Contains("Spring");
        }

        // Tjek for gearkasse
        if (componentName.Contains("Gearbox") || componentName.Contains("Transmission"))
        {
            return partName.Contains("Gear") ||
                   partName.Contains("Bearing") ||
                   partName.Contains("Shaft") ||
                   partName.Contains("Seal") ||
                   partName.Contains("Gasket");
        }

        // Generiske hydrauliske komponenter
        if (componentName.Contains("Hydraulic"))
        {
            return partName.Contains("Seal") ||
                   partName.Contains("Piston") ||
                   partName.Contains("Valve") ||
                   partName.Contains("Cylinder") ||
                   partName.Contains("Hose") ||
                   partName.Contains("Coupling");
        }

        // Generiske elektriske komponenter
        if (componentName.Contains("Electric") || componentName.Contains("Control"))
        {
            return partName.Contains("Switch") ||
                   partName.Contains("Relay") ||
                   partName.Contains("Sensor") ||
                   partName.Contains("Cable") ||
                   partName.Contains("Connector") ||
                   partName.Contains("Fuse");
        }

        // Generel mekanisk match
        return partName.Contains("Bolt") ||
               partName.Contains("Nut") ||
               partName.Contains("Washer") ||
               partName.Contains("Gasket") ||
               partName.Contains("Screw") ||
               partName.Contains("Fastener");
    }

    static int DeterminePartCount(int componentLevel, string componentName, Faker faker)
    {
        // Bestem antallet af reservedele baseret på komponentniveau og type
        int baseCount;

        switch (componentLevel)
        {
            case 3:
                baseCount = faker.Random.Int(5, 15);
                break;
            case 4:
                baseCount = faker.Random.Int(8, 25);
                break;
            case 5:
                baseCount = faker.Random.Int(3, 10);
                break;
            default:
                baseCount = faker.Random.Int(3, 8);
                break;
        }

        // Juster baseret på kompleksitet
        if (componentName.Contains("Pump") ||
            componentName.Contains("Motor") ||
            componentName.Contains("Gearbox"))
        {
            baseCount = (int)(baseCount * 1.5);
        }
        else if (componentName.Contains("Valve") ||
                 componentName.Contains("Cylinder"))
        {
            baseCount = (int)(baseCount * 1.3);
        }
        else if (componentName.Contains("Assembly") ||
                 componentName.Contains("Unit"))
        {
            baseCount = (int)(baseCount * 1.7);
        }

        return Math.Max(3, baseCount); // Min. 3 dele
    }

    static int DeterminePartQuantity(string partName, string componentName, Faker faker)
    {
        // Bestem antallet af hver reservedel baseret på deltype
        // Småting som bolte findes i større antal end hovedkomponenter

        if (partName.Contains("Bolt") ||
            partName.Contains("Nut") ||
            partName.Contains("Washer") ||
            partName.Contains("Screw"))
        {
            return faker.Random.Int(4, 32);
        }
        else if (partName.Contains("Gasket") ||
                 partName.Contains("O-Ring") ||
                 partName.Contains("Seal"))
        {
            return faker.Random.Int(1, 8);
        }
        else if (partName.Contains("Bearing") ||
                 partName.Contains("Bushing"))
        {
            return faker.Random.Int(1, 4);
        }
        else if (partName.Contains("Filter") ||
                 partName.Contains("Element"))
        {
            return faker.Random.Int(1, 2);
        }

        // Større dele findes typisk kun i enkelteksemplarer
        return faker.Random.Int(1, 2);
    }

    static string GeneratePartPosition(string partName, string componentName, Faker faker)
    {
        // Generer en realistisk positionsbeskrivelse baseret på del og komponent

        // Tjek først om vi har foruddefinerede positioner for denne komponenttype
        foreach (var entry in RealisticComponentPositions)
        {
            if (componentName.Contains(entry.Key))
            {
                return faker.PickRandom(entry.Value);
            }
        }

        // Hvis ikke, generer en standard position
        return faker.PickRandom(new[] {
                "Front", "Rear", "Top", "Bottom", "Left", "Right", "Center",
                "Upper", "Lower", "Inside", "Outside", "Middle", "Main",
                "Primary", "Secondary", "Backup", "Auxiliary", "Input", "Output"
            });
    }

    static string GenerateAssemblyNotes(string partName, string componentName, Faker faker)
    {
        // Generer realistiske bemærkninger om samling og installation
        var notes = new List<string>();

        if (partName.Contains("Bearing"))
        {
            notes.Add(faker.PickRandom(new[] {
                    "Heat to 80°C before installation",
                    "Apply bearing oil before mounting",
                    "Use proper pressing tools only",
                    "Check clearance after installation",
                    "Install with marked side facing outward"
                }));
        }
        else if (partName.Contains("Seal") || partName.Contains("Gasket"))
        {
            notes.Add(faker.PickRandom(new[] {
                    "Apply light oil before installation",
                    "Do not stretch during mounting",
                    "Ensure proper alignment with shaft",
                    "Check for damage before installation",
                    "Use manufacturer-specified lubricant only"
                }));
        }
        else if (partName.Contains("Bolt") || partName.Contains("Nut") || partName.Contains("Screw"))
        {
            notes.Add(faker.PickRandom(new[] {
                    $"Torque to {faker.Random.Int(5, 200)} Nm",
                    "Apply thread-locking compound",
                    "Install with washers in specified sequence",
                    "Check alignment before final tightening",
                    "Apply anti-seize compound on threads"
                }));
        }
        else
        {
            notes.Add(faker.PickRandom(new[] {
                    "Install according to manufacturer guidelines",
                    "Refer to technical manual for detailed assembly instructions",
                    "Check alignment before installation",
                    "Inspect for damage before mounting",
                    "Apply specified lubricant during assembly"
                }));
        }

        return notes[0];
    }

    #region SparePart
    // Modified Generate SpareParts method
    static List<SparePart> GenerateSpareParts(
       int count,
       List<SparePart> baseSpareParts,
       Guid unitGuid,
       List<Guid> manufacturerIds,
       List<Guid> categoryIds,
       List<Guid> supplierIds,
       List<Guid> locationIds,
       Faker faker)
    {
        var spareParts = new List<SparePart>();

        // If there are baseSpareParts, generate variants based on them (historical)
        if (baseSpareParts != null && baseSpareParts.Count > 0)
        {
            foreach (var basePart in baseSpareParts)
            {
                // For each basePart, generate multiple versions (1-3)
                int versionsToGenerate = faker.Random.Int(1, 3);
                for (int i = 0; i < versionsToGenerate && spareParts.Count < count; i++)
                {
                    // Check if ManufacturerGuid is null and handle it
                    Guid manufacturerId = basePart.ManufacturerGuid.HasValue ?
                        basePart.ManufacturerGuid.Value :
                        faker.PickRandom(manufacturerIds);

                    spareParts.Add(new SparePart
                    {
                        Id = Guid.NewGuid(),
                        master_id = basePart.Id,
                        UnitGuid = unitGuid,
                        SparePartNo = GenerateRevisionedPartNumber(basePart.SparePartNo, faker),
                        SparePartSerialCode = GeneratePartSerialCode(
                            GetPartTypeFromName(basePart.Name),
                            GetManufacturerNameById(manufacturerIds, manufacturerId, faker),
                            faker
                        ),
                        Name = basePart.Name,
                        Description = GenerateVariantDescription(basePart.Description, faker),
                        ManufacturerGuid = basePart.ManufacturerGuid,
                        CategoryGuid = basePart.CategoryGuid,
                        SupplierGuid = basePart.SupplierGuid,
                        LocationGuid = basePart.LocationGuid,
                        TypeNo = basePart.TypeNo,
                        Notes = faker.Random.Bool(0.6f) ? GenerateHistoricalPartNotes(basePart.Name, faker) : null,

                        // Adding measurement unit
                        MeasurementUnit = GetMeasurementUnit(GetPartTypeFromName(basePart.Name), faker),

                        // Add historical date information - historical data is older
                        CreatedDate = DateTime.Now.AddDays(-faker.Random.Int(365, 1095)),
                        LastModifiedDate = DateTime.Now.AddDays(-faker.Random.Int(180, 364)),

                        // Pricing information
                        BasePrice = basePart.BasePrice.HasValue ?
                            Math.Round(basePart.BasePrice.Value * (decimal)faker.Random.Double(0.85, 1.2), 2) : null,
                        Currency = basePart.Currency,
                        PriceDate = DateTime.Now.AddDays(-faker.Random.Int(180, 730))
                    });
                }
            }

            // If we have generated too many, trim the list
            if (spareParts.Count > count)
            {
                spareParts = spareParts.Take(count).ToList();
            }
            // If we have generated too few, generate some completely new ones
            else if (spareParts.Count < count)
            {
                int remaining = count - spareParts.Count;
                var newParts = GenerateSpareParts(
                    remaining, null, unitGuid, manufacturerIds, categoryIds, supplierIds, locationIds, faker);
                spareParts.AddRange(newParts);
            }

            return spareParts;
        }

        // Generate new spare parts from scratch

        // Define realistic spare part types
        var partTypes = new List<string>();

        // Add all part types from our various categories
        foreach (var category in ComponentsByCategory.Values)
        {
            partTypes.AddRange(category);
        }

        // Add dominant industry-specific components
        foreach (var industry in IndustrySpecificComponents.Values)
        {
            partTypes.AddRange(industry);
        }

        // Remove duplicates
        partTypes = partTypes.Distinct().ToList();

        // Generate the desired number of spare parts
        for (int i = 0; i < count; i++)
        {
            string partType = faker.PickRandom(partTypes);

            // Consider product families
            string partFamily = "";
            if (ProductFamilies.ContainsKey(GetBasePartType(partType)))
            {
                partFamily = $" {faker.PickRandom(ProductFamilies[GetBasePartType(partType)])}";
            }

            string name = GenerateRealisticPartName(partType, partFamily, faker);

            // Choose random but appropriate related data
            Guid manufacturerId = faker.PickRandom(manufacturerIds);
            Guid categoryId = GetAppropriateCategory(categoryIds, partType, faker);
            Guid supplierId = faker.PickRandom(supplierIds);
            Guid locationId = faker.PickRandom(locationIds);

            // Generate special numbers and codes
            string partNumber = GeneratePartNumber(partType, faker);
            string serialCode = GeneratePartSerialCode(
                partType,
                GetManufacturerNameById(manufacturerIds, manufacturerId, faker),
                faker
            );
            string typeNumber = GenerateTypeNumber(partType, faker);

            // Generate part notes with extended search terms
            string notes = faker.Random.Bool(0.7f) ?
                EnhanceWithSearchTerms(GeneratePartNotes(name, partType, faker), partType, faker) : null;

            // Generate description with additional technical information
            string description = EnhancePartDescription(
                GeneratePartDescription(name, partType, faker),
                partType,
                faker
            );

            // Create the spare part
            var sparePart = new SparePart
            {
                Id = Guid.NewGuid(),
                master_id = null,
                UnitGuid = unitGuid,
                SparePartNo = partNumber,
                SparePartSerialCode = serialCode,
                Name = name,
                Description = description,
                ManufacturerGuid = manufacturerId,
                CategoryGuid = categoryId,
                SupplierGuid = supplierId,
                LocationGuid = locationId,
                TypeNo = typeNumber,
                Notes = notes,

                // Add measurement unit
                MeasurementUnit = GetMeasurementUnit(partType, faker),

                // Date information
                CreatedDate = DateTime.Now.AddDays(-faker.Random.Int(1, 180)),
                LastModifiedDate = faker.Random.Bool(0.7f) ?
                    DateTime.Now.AddDays(-faker.Random.Int(1, 30)) : null,

                // Pricing information
                BasePrice = faker.Random.Bool(0.85f) ?
                    (decimal?)Math.Round(faker.Random.Decimal(1, 10000), 2) : null,
                Currency = faker.Random.Bool(0.9f) ?
                    faker.PickRandom(new[] { "USD", "EUR", "DKK", "GBP", "JPY", "SEK", "NOK" }) : null,
                PriceDate = faker.Random.Bool(0.8f) ?
                    DateTime.Now.AddDays(-faker.Random.Int(1, 365)) : null
            };

            spareParts.Add(sparePart);
        }

        return spareParts;
    }

    // Modified Generate Historical Spare Parts
    // This follows the revised approach: first historical, then active
    static List<SparePart> GenerateHistoricalSpareParts(
        int count,
        List<SparePart> activeSpareParts,
        Guid unitGuid,
        List<Guid> manufacturerIds,
        List<Guid> categoryIds,
        List<Guid> supplierIds,
        List<Guid> locationIds,
        Faker faker)
    {
        var historicalSpareParts = new List<SparePart>();

        // For each active part, create a realistic evolution history
        foreach (var activePart in activeSpareParts)
        {
            // Only create history for some parts (70%)
            if (faker.Random.Bool(0.7f))
            {
                // How many historical versions (1-5)
                int historyCount = faker.Random.Int(1, 5);

                // Create the historical chain, starting from the oldest
                SparePart currentPart = null;

                for (int version = 1; version <= historyCount && historicalSpareParts.Count < count; version++)
                {
                    bool isFirstVersion = (version == 1);
                    bool isLatestHistorical = (version == historyCount);

                    // Create a historical version
                    var historicalPart = new SparePart
                    {
                        Id = Guid.NewGuid(),
                        // First version has no master, others point to active part
                        master_id = isFirstVersion ? null : activePart.Id,
                        UnitGuid = unitGuid,
                        SparePartNo = isFirstVersion ?
                            activePart.SparePartNo :
                            AddVersionToPartNumber(activePart.SparePartNo, version, faker),
                        SparePartSerialCode = activePart.SparePartSerialCode,

                        // Apply realistic changes to fields over time
                        Name = MakeHistoricalVariation(
                            isFirstVersion ? activePart.Name : currentPart.Name,
                            "Name",
                            version,
                            faker
                        ),
                        Description = MakeHistoricalVariation(
                            isFirstVersion ? activePart.Description : currentPart.Description,
                            "Description",
                            version,
                            faker
                        ),

                        // Usually keep the same relationships
                        ManufacturerGuid = faker.Random.Bool(0.9f) ?
                            activePart.ManufacturerGuid :
                            faker.PickRandom(manufacturerIds),
                        CategoryGuid = activePart.CategoryGuid,
                        SupplierGuid = faker.Random.Bool(0.8f) ?
                            activePart.SupplierGuid :
                            faker.PickRandom(supplierIds),
                        LocationGuid = faker.Random.Bool(0.7f) ?
                            activePart.LocationGuid :
                            faker.PickRandom(locationIds),

                        TypeNo = activePart.TypeNo,
                        Notes = MakeHistoricalVariation(
                            isFirstVersion ? activePart.Notes : currentPart.Notes,
                            "Notes",
                            version,
                            faker
                        ),

                        // Measurement unit usually stays the same
                        MeasurementUnit = activePart.MeasurementUnit,

                        // Set dates to show realistic evolution
                        // Older versions have older timestamps
                        CreatedDate = DateTime.Now.AddDays(-365 * (historyCount - version + 3)),
                        LastModifiedDate = DateTime.Now.AddDays(-365 * (historyCount - version + 2) - faker.Random.Int(1, 60)),

                        // Price tends to increase over time
                        BasePrice = activePart.BasePrice.HasValue ?
                            Math.Round(activePart.BasePrice.Value * (decimal)(0.7 + (version * 0.05)), 2) :
                            null,
                        Currency = activePart.Currency,
                        PriceDate = DateTime.Now.AddDays(-365 * (historyCount - version + 2))
                    };

                    historicalSpareParts.Add(historicalPart);
                    currentPart = historicalPart;
                }

                // Make sure active part has the latest timestamp
                activePart.CreatedDate = DateTime.Now.AddDays(-faker.Random.Int(1, 180));
                if (activePart.LastModifiedDate.HasValue)
                {
                    activePart.LastModifiedDate = DateTime.Now.AddDays(-faker.Random.Int(0, 30));
                }
            }
        }

        // If we've generated too many historical parts, trim the list
        if (historicalSpareParts.Count > count)
        {
            historicalSpareParts = historicalSpareParts.Take(count).ToList();
        }

        // If we need more to reach our target count, generate some standalone historical parts
        if (historicalSpareParts.Count < count)
        {
            int remainingCount = count - historicalSpareParts.Count;

            // Create remaining historical parts without linkage to active parts
            var standaloneHistorical = GenerateSpareParts(
                remainingCount, null, unitGuid, manufacturerIds, categoryIds, supplierIds, locationIds, faker);

            // Mark them as historical by setting older dates
            foreach (var part in standaloneHistorical)
            {
                part.CreatedDate = DateTime.Now.AddDays(-faker.Random.Int(365, 1825));
                part.LastModifiedDate = faker.Random.Bool(0.8f) ?
                    DateTime.Now.AddDays(-faker.Random.Int(180, 364)) : null;
            }

            historicalSpareParts.AddRange(standaloneHistorical);
        }

        return historicalSpareParts;
    }

    // Helper method to create realistic variations for historical records
    static string MakeHistoricalVariation(string original, string fieldType, int version, Faker faker)
    {
        if (string.IsNullOrEmpty(original)) return original;

        // Early versions have more differences
        float changeChance = Math.Max(0.1f, 0.7f - (version * 0.1f));

        if (!faker.Random.Bool(changeChance))
            return original;

        // Make different types of changes based on field type
        switch (fieldType)
        {
            case "Name":
                // For names, make minor wording changes
                return MakeNameVariation(original, faker);

            case "Description":
                // For descriptions, add/remove technical details
                return MakeDescriptionVariation(original, faker);

            case "Notes":
                // For notes, change maintenance instructions, etc.
                return MakeNotesVariation(original, faker);

            default:
                return original;
        }
    }

    static string MakeNameVariation(string name, Faker faker)
    {
        if (string.IsNullOrEmpty(name)) return name;

        int changeType = faker.Random.Int(1, 5);

        switch (changeType)
        {
            case 1: // Add/remove qualifier
                var qualifiers = new[] { "Standard ", "Basic ", "Premium ", "High-Performance ", "Industrial " };
                if (qualifiers.Any(q => name.StartsWith(q)))
                    return name.Substring(name.IndexOf(' ') + 1); // Remove qualifier
                else
                    return faker.PickRandom(qualifiers) + name; // Add qualifier

            case 2: // Change formatting/abbreviations
                if (name.Contains("Assembly"))
                    return name.Replace("Assembly", "Assy");
                else if (name.Contains("Assy"))
                    return name.Replace("Assy", "Assembly");
                else if (name.Contains("with"))
                    return name.Replace("with", "w/");
                else if (name.Contains("w/"))
                    return name.Replace("w/", "with");
                return name;

            case 3: // Capitalization differences
                if (name.Length > 5)
                {
                    var words = name.Split(' ');
                    if (words.Length > 1)
                    {
                        words[faker.Random.Int(0, words.Length - 1)] =
                            words[faker.Random.Int(0, words.Length - 1)].ToUpper();
                        return string.Join(" ", words);
                    }
                }
                return name;

            case 4: // Spelling "correction"
                if (name.Contains("ise"))
                    return name.Replace("ise", "ize");
                else if (name.Contains("ize"))
                    return name.Replace("ize", "ise");
                return name;

            case 5: // Add/remove hyphen
                if (name.Contains(" ") && !name.Contains("-"))
                {
                    var words = name.Split(' ');
                    if (words.Length > 1)
                    {
                        int idx = faker.Random.Int(0, words.Length - 2);
                        return name.Replace($"{words[idx]} {words[idx + 1]}", $"{words[idx]}-{words[idx + 1]}");
                    }
                }
                else if (name.Contains("-"))
                {
                    return name.Replace("-", " ");
                }
                return name;
        }

        return name;
    }

    static string MakeDescriptionVariation(string description, Faker faker)
    {
        if (string.IsNullOrEmpty(description)) return description;

        int changeType = faker.Random.Int(1, 4);

        switch (changeType)
        {
            case 1: // Add technical detail
                return description + $" {faker.PickRandom(new[] {
                    "Suitable for high-temperature applications.",
                    "Recommended for continuous operation.",
                    "Complies with international standards.",
                    "Features enhanced durability coating.",
                    "Upgraded design for improved efficiency."
                })}";

            case 2: // Remove a sentence if description is long enough
                if (description.Contains(". "))
                {
                    var sentences = description.Split(new[] { ". " }, StringSplitOptions.None);
                    if (sentences.Length > 1)
                    {
                        int toRemove = faker.Random.Int(0, sentences.Length - 1);
                        return string.Join(". ", sentences.Where((s, i) => i != toRemove)) +
                               (description.EndsWith(".") ? "" : ".");
                    }
                }
                return description;

            case 3: // Change measurement value
                if (description.Contains(" mm") || description.Contains(" kg") ||
                    description.Contains(" bar") || description.Contains(" °C"))
                {
                    // Find numbers in the description
                    var words = description.Split(' ');
                    for (int i = 0; i < words.Length; i++)
                    {
                        if (int.TryParse(words[i], out int number))
                        {
                            // Replace with slightly different number
                            words[i] = (number + faker.Random.Int(-5, 5)).ToString();
                            return string.Join(" ", words);
                        }
                    }
                }
                return description;

            case 4: // Change material specification
                foreach (var material in MaterialsByComponent.Values.SelectMany(m => m))
                {
                    if (description.Contains(material))
                    {
                        // Get a random different material from the same category
                        foreach (var entry in MaterialsByComponent)
                        {
                            if (entry.Value.Contains(material))
                            {
                                var newMaterial = faker.PickRandom(
                                    entry.Value.Where(m => m != material).ToList());
                                return description.Replace(material, newMaterial);
                            }
                        }
                    }
                }
                return description;
        }

        return description;
    }

    static string MakeNotesVariation(string notes, Faker faker)
    {
        if (string.IsNullOrEmpty(notes)) return notes;

        int changeType = faker.Random.Int(1, 3);

        switch (changeType)
        {
            case 1: // Change maintenance interval
                if (notes.Contains("every") && notes.Contains("months"))
                {
                    var parts = notes.Split(' ');
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        if (parts[i] == "every" && int.TryParse(parts[i + 1], out int interval))
                        {
                            // Change the interval
                            parts[i + 1] = (interval + faker.Random.Int(-3, 3)).ToString();
                            return string.Join(" ", parts);
                        }
                    }
                }
                return notes;

            case 2: // Change inventory level recommendation
                if (notes.Contains("stock") && notes.Contains("minimum"))
                {
                    var parts = notes.Split(' ');
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        if (parts[i] == "minimum" && int.TryParse(parts[i + 1], out int level))
                        {
                            // Change the recommended stock level
                            parts[i + 1] = (level + faker.Random.Int(-1, 2)).ToString();
                            return string.Join(" ", parts);
                        }
                    }
                }
                return notes;

            case 3: // Add/change search keywords
                if (notes.Contains("Keywords:"))
                {
                    // Find the keywords section
                    int keywordStart = notes.IndexOf("Keywords:") + 10;
                    int keywordEnd = notes.IndexOf(".", keywordStart);
                    if (keywordEnd == -1) keywordEnd = notes.Length;

                    string beforeKeywords = notes.Substring(0, keywordStart);
                    string afterKeywords = keywordEnd < notes.Length ?
                        notes.Substring(keywordEnd) : "";

                    // Generate new keywords
                    var keywords = faker.Random.ListItems(CommonSearchTerms, faker.Random.Int(2, 5));

                    return beforeKeywords + string.Join(", ", keywords) + afterKeywords;
                }
                return notes;
        }

        return notes;
    }

    // Helper method to add version to part number for historical data
    static string AddVersionToPartNumber(string partNumber, int version, Faker faker)
    {
        var versionFormats = new Func<string, int, string>[] {
            (pn, v) => $"{pn}-V{v}",
            (pn, v) => $"{pn}.{v}",
            (pn, v) => $"{pn}/Rev{v}",
            (pn, v) => $"{pn}_v{v}"
        };

        return faker.PickRandom(versionFormats)(partNumber, version);
    }

    // Helper to get measurement unit based on part type
    static string GetMeasurementUnit(string partType, Faker faker)
    {
        if (partType.Contains("Valve") || partType.Contains("Pipe"))
        {
            return faker.PickRandom(new[] { "mm", "inches", "DN" });
        }
        else if (partType.Contains("Pump") || partType.Contains("Compressor"))
        {
            return faker.PickRandom(new[] { "m³/h", "l/min", "CFM" });
        }
        else if (partType.Contains("Motor"))
        {
            return faker.PickRandom(new[] { "kW", "HP", "rpm" });
        }
        else if (partType.Contains("Bearing") || partType.Contains("Gear"))
        {
            return faker.PickRandom(new[] { "mm", "inches", "N" });
        }
        else if (partType.Contains("Weight") || partType.Contains("Load"))
        {
            return faker.PickRandom(new[] { "kg", "ton", "lbs" });
        }
        else if (partType.Contains("Pressure") || partType.Contains("Valve"))
        {
            return faker.PickRandom(new[] { "bar", "psi", "kPa" });
        }
        else if (partType.Contains("Temperature") || partType.Contains("Heating"))
        {
            return faker.PickRandom(new[] { "°C", "°F", "K" });
        }
        else if (partType.Contains("Seal") || partType.Contains("Gasket"))
        {
            return faker.PickRandom(new[] { "mm", "inches", "set" });
        }
        else
        {
            return faker.PickRandom(new[] { "pcs", "set", "unit", "assy" });
        }
    }

    // Helper to enhance notes with additional search terms
    static string EnhanceWithSearchTerms(string notes, string partType, Faker faker)
    {
        if (string.IsNullOrEmpty(notes))
        {
            notes = "No specific notes.";
        }

        // Always add search terms for better search testing
        var relevantSearchTerms = new List<string>();

        // Add generic terms
        relevantSearchTerms.Add(faker.PickRandom(CommonSearchTerms));
        relevantSearchTerms.Add(faker.PickRandom(CommonSearchTerms));

        // Add part-specific terms
        if (partType.Contains("Valve"))
            relevantSearchTerms.Add(faker.PickRandom(new[] { "flow control", "throttling", "isolation", "check valve" }));
        else if (partType.Contains("Pump"))
            relevantSearchTerms.Add(faker.PickRandom(new[] { "centrifugal", "positive displacement", "water pump", "oil pump" }));
        else if (partType.Contains("Motor"))
            relevantSearchTerms.Add(faker.PickRandom(new[] { "electric", "servo", "speed control", "variable frequency" }));
        else if (partType.Contains("Bearing"))
            relevantSearchTerms.Add(faker.PickRandom(new[] { "roller", "ball", "thrust", "sleeve", "pillow block" }));
        else if (partType.Contains("Seal"))
            relevantSearchTerms.Add(faker.PickRandom(new[] { "mechanical", "lip", "o-ring", "face", "radial" }));

        // Add brand-specific terms based on manufacturer
        relevantSearchTerms.Add(faker.PickRandom(new[] {
            "Siemens part", "SKF equivalent", "Grundfos replacement", "ABB compatible",
            "Danfoss alternative", "WILO substitute", "Bosch component", "Emerson part"
        }));

        // Add maintenance-related terms
        if (faker.Random.Bool(0.3f))
            relevantSearchTerms.Add(faker.PickRandom(new[] {
                "preventive maintenance", "critical spare", "long lead time", "emergency stock",
                "planned shutdown", "recommend stock level", "common failure", "high wear item"
            }));

        // Add documentation references
        if (faker.Random.Bool(0.4f))
        {
            var docTypes = new[] {
                "Manual", "Datasheet", "Catalog", "Drawing", "P&ID", "Specification Sheet",
                "IOM Manual", "Technical Bulletin", "Service Guideline", "Spare Parts List"
            };

            string docType = faker.PickRandom(docTypes);
            string docNumber = faker.Random.String2(2, "ABCDEFGHIJKLMNOPQRSTUVWXYZ") +
                              "-" + faker.Random.Number(1000, 9999);

            relevantSearchTerms.Add($"{docType} {docNumber}");
        }

        // Add the terms to the notes
        if (!notes.Contains("Keywords:"))
        {
            notes += $" Keywords: {string.Join(", ", relevantSearchTerms)}.";
        }

        return notes;
    }

    // Helper to enhance part description with additional technical information
    static string EnhancePartDescription(string description, string partType, Faker faker)
    {
        if (string.IsNullOrEmpty(description))
            return description;

        // Add wear information for better maintenance search
        if (faker.Random.Bool(0.3f))
        {
            var wearInfo = new[] {
                $"Typical wear pattern: {faker.PickRandom(new[] {"surface erosion", "cavitation", "pitting", "fatigue cracks", "deformation", "scoring", "galling"})}.",
                $"Expected service life: {faker.Random.Int(1, 10)} years under normal operating conditions.",
                $"Inspection interval: {faker.Random.Int(3, 24)} months.",
                $"Replace when {faker.PickRandom(new[] {"clearance exceeds", "erosion reaches", "cracks appear", "leakage occurs", "performance drops"})} {faker.Random.Int(2, 15)}%.",
                $"Critical wear limit: {faker.Random.Double(0.1, 5.0).ToString("F2")} mm."
            };

            description += " " + faker.PickRandom(wearInfo);
        }

        return description;
    }

    // Method to add edge cases for search testing
    static List<SparePart> AddSearchEdgeCases(
        List<SparePart> baseParts,
        Guid unitGuid,
        List<Guid> manufacturerIds,
        List<Guid> categoryIds,
        List<Guid> supplierIds,
        List<Guid> locationIds,
        Faker faker)
    {
        var edgeCaseParts = new List<SparePart>();

        // 1. Parts with special characters in names (50 examples)
        for (int i = 0; i < 50 && i < baseParts.Count; i++)
        {
            var basePart = baseParts[i];

            edgeCaseParts.Add(new SparePart
            {
                Id = Guid.NewGuid(),
                UnitGuid = unitGuid,
                SparePartNo = basePart.SparePartNo + "-$%#",
                SparePartSerialCode = basePart.SparePartSerialCode + "/ÆØÅ",
                Name = basePart.Name + " (Special: & / - _ + @ € £ ¥)",
                Description = basePart.Description,
                ManufacturerGuid = basePart.ManufacturerGuid,
                CategoryGuid = basePart.CategoryGuid,
                SupplierGuid = basePart.SupplierGuid,
                LocationGuid = basePart.LocationGuid,
                TypeNo = basePart.TypeNo,
                Notes = basePart.Notes,
                MeasurementUnit = basePart.MeasurementUnit,
                CreatedDate = DateTime.Now.AddDays(-faker.Random.Int(1, 30)),
                LastModifiedDate = DateTime.Now.AddDays(-faker.Random.Int(0, 7)),
                BasePrice = basePart.BasePrice,
                Currency = basePart.Currency,
                PriceDate = basePart.PriceDate
            });
        }

        // 2. Very long descriptions that test search indexing limits
        // But using industry-specific text instead of lorem ipsum
        for (int i = 50; i < 100 && i < baseParts.Count; i++)
        {
            var basePart = baseParts[i];

            // Industry-specific long text instead of lorem ipsum
            string longDesc = GenerateLongTechnicalDescription(basePart.Name, 2000, faker);

            edgeCaseParts.Add(new SparePart
            {
                Id = Guid.NewGuid(),
                UnitGuid = unitGuid,
                SparePartNo = basePart.SparePartNo + "-LONG",
                SparePartSerialCode = basePart.SparePartSerialCode + "/DETAIL",
                Name = basePart.Name + " (Detailed Specification)",
                Description = longDesc,
                ManufacturerGuid = basePart.ManufacturerGuid,
                CategoryGuid = basePart.CategoryGuid,
                SupplierGuid = basePart.SupplierGuid,
                LocationGuid = basePart.LocationGuid,
                TypeNo = basePart.TypeNo,
                Notes = basePart.Notes,
                MeasurementUnit = basePart.MeasurementUnit,
                CreatedDate = DateTime.Now.AddDays(-faker.Random.Int(1, 30)),
                LastModifiedDate = DateTime.Now.AddDays(-faker.Random.Int(0, 7)),
                BasePrice = basePart.BasePrice,
                Currency = basePart.Currency,
                PriceDate = basePart.PriceDate
            });
        }

        // 3. Add parts with similar names/numbers but slight differences (for testing search precision)
        for (int i = 100; i < 150 && i < baseParts.Count; i++)
        {
            var basePart = baseParts[i];

            // Create a similar part number with subtle differences
            string similarPartNo = CreateSimilarPartNumber(basePart.SparePartNo, faker);

            edgeCaseParts.Add(new SparePart
            {
                Id = Guid.NewGuid(),
                UnitGuid = unitGuid,
                SparePartNo = similarPartNo,
                SparePartSerialCode = basePart.SparePartSerialCode.Replace("1", "I").Replace("0", "O"), // Common confusion
                Name = basePart.Name + " (Similar)",
                Description = basePart.Description,
                ManufacturerGuid = basePart.ManufacturerGuid,
                CategoryGuid = basePart.CategoryGuid,
                SupplierGuid = basePart.SupplierGuid,
                LocationGuid = basePart.LocationGuid,
                TypeNo = basePart.TypeNo,
                Notes = basePart.Notes,
                MeasurementUnit = basePart.MeasurementUnit,
                CreatedDate = DateTime.Now.AddDays(-faker.Random.Int(1, 30)),
                LastModifiedDate = DateTime.Now.AddDays(-faker.Random.Int(0, 7)),
                BasePrice = basePart.BasePrice,
                Currency = basePart.Currency,
                PriceDate = basePart.PriceDate
            });
        }

        return edgeCaseParts;
    }

    // Helper to create a similar part number with subtle differences
    static string CreateSimilarPartNumber(string original, Faker faker)
    {
        int changeType = faker.Random.Int(1, 5);

        switch (changeType)
        {
            case 1: // Replace number with similar-looking letter
                return original.Replace("1", "I").Replace("0", "O").Replace("5", "S").Replace("8", "B");

            case 2: // Swap two adjacent characters
                if (original.Length > 2)
                {
                    int pos = faker.Random.Int(0, original.Length - 2);
                    return original.Substring(0, pos) +
                           original[pos + 1] + original[pos] +
                           original.Substring(pos + 2);
                }
                return original;

            case 3: // Change delimiter
                if (original.Contains("-"))
                    return original.Replace("-", ".");
                else if (original.Contains("."))
                    return original.Replace(".", "-");
                else if (original.Contains("/"))
                    return original.Replace("/", "-");
                else if (original.Length > 3)
                    return original.Substring(0, 3) + "-" + original.Substring(3);
                return original;

            case 4: // Add/remove space
                if (original.Contains(" "))
                    return original.Replace(" ", "");
                else if (original.Length > 3)
                    return original.Substring(0, 3) + " " + original.Substring(3);
                return original;

            case 5: // Change case
                return original.ToUpper() != original ? original.ToUpper() : original.ToLower();
        }

        return original;
    }

    // Helper to generate realistic technical content for long descriptions
    static string GenerateLongTechnicalDescription(string partName, int approxLength, Faker faker)
    {
        var partType = GetPartTypeFromName(partName);
        var sections = new List<string>();

        // Section 1: Detailed specifications
        sections.Add($"Technical Specifications for {partName}: " + string.Join(" ", Enumerable.Range(0, 5).Select(_ =>
            GenerateTechnicalSpecification(partType, faker))));

        // Section 2: Installation instructions
        sections.Add($"Installation Guidelines: " + string.Join(" ", Enumerable.Range(0, 7).Select(_ =>
            GenerateInstallationStep(partType, faker))));

        // Section 3: Maintenance procedures
        sections.Add($"Maintenance Requirements: " + string.Join(" ", Enumerable.Range(0, 5).Select(_ =>
            GenerateMaintenanceStep(partType, faker))));

        // Section 4: Compatibility information
        sections.Add($"Compatibility Information: " + string.Join(" ", Enumerable.Range(0, 4).Select(_ =>
            GenerateCompatibilityInfo(partType, faker))));

        // Section 5: Troubleshooting common issues
        sections.Add($"Troubleshooting Guide: " + string.Join(" ", Enumerable.Range(0, 6).Select(_ =>
            GenerateTroubleshootingTip(partType, faker))));

        // Repeat sections until we reach approximate desired length
        var result = new StringBuilder();
        int currentIndex = 0;

        while (result.Length < approxLength)
        {
            result.AppendLine(sections[currentIndex % sections.Count]);
            result.AppendLine();
            currentIndex++;
        }

        string finalDescription = result.ToString();
        return finalDescription.Length > 2000 ? finalDescription.Substring(0, 2000) : finalDescription;
    }

    // Helpers to generate realistic technical content
    static string GenerateTechnicalSpecification(string partType, Faker faker)
    {
        if (partType.Contains("Valve"))
        {
            return faker.PickRandom(new[] {
                "Maximum working pressure: {0} bar at temperature {1}°C.",
                "Body material: {2} with {3} trim.",
                "Face-to-face dimensions according to {4}.",
                "Flow coefficient Kv: {5} m³/h.",
                "Leakage class: {6} as per {7}.",
                "Connection type: {8} with {9} rating."
            }).Replace("{0}", faker.Random.Int(16, 420).ToString())
              .Replace("{1}", faker.Random.Int(-20, 550).ToString())
              .Replace("{2}", faker.PickRandom(MaterialsByComponent["Valve"]))
              .Replace("{3}", faker.PickRandom(MaterialsByComponent["Valve"]))
              .Replace("{4}", faker.PickRandom(new[] { "EN 558", "ASME B16.10", "API 6D", "DIN 3202" }))
              .Replace("{5}", faker.Random.Double(0.1, 2500).ToString("F1"))
              .Replace("{6}", faker.PickRandom(new[] { "IV", "V", "VI" }))
              .Replace("{7}", faker.PickRandom(new[] { "EN 12266-1", "ANSI/FCI 70-2", "IEC 60534-4" }))
              .Replace("{8}", faker.PickRandom(new[] { "Flanged", "Welded", "Threaded", "Clamp" }))
              .Replace("{9}", faker.PickRandom(new[] { "PN16", "PN25", "PN40", "PN100", "Class 150", "Class 300", "Class 600" }));
        }
        else if (partType.Contains("Pump"))
        {
            return faker.PickRandom(new[] {
                "Flow rate: {0} m³/h at {1} bar differential pressure.",
                "Motor power: {2} kW, {3} V, {4} Hz.",
                "Impeller diameter: {5} mm, {6}.",
                "NPSH required: {7} m at maximum flow.",
                "Efficiency: {8}% at duty point.",
                "Maximum fluid temperature: {9}°C."
            }).Replace("{0}", faker.Random.Int(5, 2000).ToString())
              .Replace("{1}", faker.Random.Double(2, 50).ToString("F1"))
              .Replace("{2}", faker.Random.Double(0.75, 500).ToString("F1"))
              .Replace("{3}", faker.PickRandom(new[] { "230", "400", "415", "690" }))
              .Replace("{4}", faker.PickRandom(new[] { "50", "60" }))
              .Replace("{5}", faker.Random.Int(100, 500).ToString())
              .Replace("{6}", faker.PickRandom(new[] { "cast iron", "bronze", "stainless steel", "duplex" }))
              .Replace("{7}", faker.Random.Double(1, 15).ToString("F1"))
              .Replace("{8}", faker.Random.Int(60, 95).ToString())
              .Replace("{9}", faker.Random.Int(80, 350).ToString());
        }
        else if (partType.Contains("Bearing"))
        {
            return faker.PickRandom(new[] {
                "Dynamic load rating: {0} kN, static load rating: {1} kN.",
                "Inner diameter: {2} mm, outer diameter: {3} mm, width: {4} mm.",
                "Maximum speed: {5} rpm for grease lubrication, {6} rpm for oil lubrication.",
                "Limiting speed: {7} rpm, reference speed: {8} rpm.",
                "Precision class: {9}, cage material: {10}.",
                "Clearance class: {11}, bearing design: {12}."
            }).Replace("{0}", faker.Random.Int(10, 1000).ToString())
              .Replace("{1}", faker.Random.Int(5, 2000).ToString())
              .Replace("{2}", faker.Random.Int(10, 150).ToString())
              .Replace("{3}", faker.Random.Int(30, 300).ToString())
              .Replace("{4}", faker.Random.Int(10, 100).ToString())
              .Replace("{5}", faker.Random.Int(1000, 5000).ToString())
              .Replace("{6}", faker.Random.Int(2000, 8000).ToString())
              .Replace("{7}", faker.Random.Int(3000, 10000).ToString())
              .Replace("{8}", faker.Random.Int(1000, 6000).ToString())
              .Replace("{9}", faker.PickRandom(new[] { "P6", "P5", "P4", "P2" }))
              .Replace("{10}", faker.PickRandom(new[] { "steel", "brass", "polyamide", "PEEK" }))
              .Replace("{11}", faker.PickRandom(new[] { "C2", "C3", "C4", "C5" }))
              .Replace("{12}", faker.PickRandom(new[] { "open", "sealed", "shielded" }));
        }
        else if (partType.Contains("Seal") || partType.Contains("Gasket"))
        {
            return faker.PickRandom(new[] {
                "Pressure rating: {0} bar, temperature range: {1}°C to {2}°C.",
                "Material: {3}, hardness: {4} Shore A.",
                "Media compatibility: {5}, design standard: {6}.",
                "Face materials: {7} vs {8}, secondary seals: {9}.",
                "Maximum surface speed: {10} m/s, PV limit: {11} N/mm² × m/s."
            }).Replace("{0}", faker.Random.Int(10, 600).ToString())
              .Replace("{1}", faker.Random.Int(-50, -10).ToString())
              .Replace("{2}", faker.Random.Int(100, 450).ToString())
              .Replace("{3}", faker.PickRandom(MaterialsByComponent["Seal"]))
              .Replace("{4}", faker.Random.Int(60, 95).ToString())
              .Replace("{5}", faker.PickRandom(new[] { "Water", "Oil", "Chemicals", "Steam", "Gas", "Slurries" }))
              .Replace("{6}", faker.PickRandom(new[] { "EN 12756", "API 682", "DIN 24960", "ISO 3069" }))
              .Replace("{7}", faker.PickRandom(new[] { "Carbon", "Tungsten carbide", "Silicon carbide", "Ceramic" }))
              .Replace("{8}", faker.PickRandom(new[] { "Tungsten carbide", "Silicon carbide", "Ceramic", "Stainless steel" }))
              .Replace("{9}", faker.PickRandom(new[] { "FKM", "EPDM", "NBR", "PTFE", "Kalrez" }))
              .Replace("{10}", faker.Random.Int(5, 50).ToString())
              .Replace("{11}", faker.Random.Double(0.1, 10).ToString("F2"));
        }
        else
        {
            return $"Specification: {faker.Commerce.ProductAdjective()} rated for industrial use with {faker.Random.Int(1, 10)} year warranty period when properly maintained.";
        }
    }

    static string GenerateInstallationStep(string partType, Faker faker)
    {
        if (partType.Contains("Valve"))
        {
            return faker.PickRandom(new[] {
                "Ensure pipeline is clean and free of debris before installation.",
                "Check flow direction arrow matches the intended flow direction.",
                "Use appropriate gaskets compatible with the process media.",
                "Tighten bolts in cross pattern to the specified torque of {0} Nm.",
                "Allow sufficient clearance for actuator operation and maintenance.",
                "Support the valve appropriately to prevent pipeline strain.",
                "Install in a position allowing access to all adjustment points.",
                "Use thread sealant on threaded connections as specified in manual."
            }).Replace("{0}", faker.Random.Int(20, 250).ToString());
        }
        else if (partType.Contains("Pump"))
        {
            return faker.PickRandom(new[] {
                "Ensure foundation is level and capable of supporting the weight.",
                "Use flexible connections on suction and discharge to reduce vibration transmission.",
                "Maintain minimum straight pipe lengths of {0}D on suction side.",
                "Fill pump with liquid before first startup to prevent dry running.",
                "Check rotation direction before coupling to the driven equipment.",
                "Align coupling to within {1} mm parallel and {2} mm angular tolerance.",
                "Install drain pan and piping for mechanical seal leakage if required.",
                "Provide adequate space around the pump for maintenance access."
            }).Replace("{0}", faker.Random.Int(5, 15).ToString())
              .Replace("{1}", faker.Random.Double(0.05, 0.2).ToString("F2"))
              .Replace("{2}", faker.Random.Double(0.05, 0.2).ToString("F2"));
        }
        else if (partType.Contains("Bearing"))
        {
            return faker.PickRandom(new[] {
                "Clean shaft and housing thoroughly before installation.",
                "Heat bearing to {0}°C for interference fit mounting using induction heater.",
                "Apply mounting force only to the ring being fitted (never through rolling elements).",
                "Use proper installation tools to avoid damage to bearing surfaces.",
                "Verify correct clearance after mounting according to specification.",
                "Fill with recommended lubricant to the specified level.",
                "Rotate by hand after installation to verify smooth operation.",
                "Allow bearing to cool to ambient temperature before operation."
            }).Replace("{0}", faker.Random.Int(80, 120).ToString());
        }
        else
        {
            return faker.PickRandom(new[] {
                "Follow manufacturer's instructions for proper installation sequence.",
                "Use calibrated torque tools for all fasteners.",
                "Verify all clearances meet specification before final assembly.",
                "Perform function test after installation before full commissioning.",
                "Document all installation parameters for future reference.",
                "Ensure all safety guards and devices are in place before operation.",
                "Conduct baseline measurements for condition monitoring if applicable."
            });
        }
    }

    static string GenerateMaintenanceStep(string partType, Faker faker)
    {
        if (partType.Contains("Valve"))
        {
            return faker.PickRandom(new[] {
                "Inspect valve stem for scoring or excessive wear every {0} months.",
                "Replace packing gland when leakage becomes evident or every {1} years.",
                "Check actuator adjustment and operation during scheduled outages.",
                "Lubricate moving parts according to maintenance schedule.",
                "Test valve operation through full stroke periodically to prevent seizure.",
                "Inspect seat and disc/ball for erosion or damage during overhauls.",
                "Replace gaskets whenever valve is disassembled."
            }).Replace("{0}", faker.Random.Int(3, 24).ToString())
              .Replace("{1}", faker.Random.Int(2, 10).ToString());
        }
        else if (partType.Contains("Pump"))
        {
            return faker.PickRandom(new[] {
                "Monitor vibration levels monthly, investigate if exceeding {0} mm/s RMS.",
                "Replace mechanical seal every {1} operating hours or if leakage exceeds {2} drops per minute.",
                "Check coupling alignment every {3} months and after any maintenance.",
                "Lubricate bearings every {4} operating hours with specified grease.",
                "Inspect impeller for cavitation damage or wear during overhauls.",
                "Monitor differential pressure to detect internal wear or fouling.",
                "Check motor winding insulation resistance annually."
            }).Replace("{0}", faker.Random.Double(4.5, 11.2).ToString("F1"))
              .Replace("{1}", faker.Random.Int(8000, 25000).ToString())
              .Replace("{2}", faker.Random.Int(3, 20).ToString())
              .Replace("{3}", faker.Random.Int(3, 12).ToString())
              .Replace("{4}", faker.Random.Int(2000, 8000).ToString());
        }
        else if (partType.Contains("Bearing"))
        {
            return faker.PickRandom(new[] {
                "Relubricate with {0} grams of specified grease every {1} operating hours.",
                "Monitor bearing temperature, investigate if rise exceeds {2}°C above ambient.",
                "Listen for unusual noise using ultrasonic equipment during operation.",
                "Check seal condition and replace if wear is evident.",
                "Analyze lubricant condition every {3} months for contamination and degradation.",
                "Replace bearing at {4} of calculated L10 life or if condition monitoring indicates.",
                "Clean and inspect housing during scheduled maintenance."
            }).Replace("{0}", faker.Random.Int(20, 500).ToString())
              .Replace("{1}", faker.Random.Int(1000, 8000).ToString())
              .Replace("{2}", faker.Random.Int(15, 40).ToString())
              .Replace("{3}", faker.Random.Int(3, 12).ToString())
              .Replace("{4}", faker.PickRandom(new[] { "70%", "80%", "90%" }));
        }
        else
        {
            return faker.PickRandom(new[] {
                "Perform visual inspection every {0} months for signs of wear or damage.",
                "Clean exterior surfaces to prevent buildup of contaminants.",
                "Document all maintenance activities and findings in equipment history.",
                "Replace at end of service life or when repair costs exceed replacement value.",
                "Follow manufacturer's recommended preventive maintenance schedule.",
                "Keep spare parts in appropriate storage conditions until needed."
            }).Replace("{0}", faker.Random.Int(1, 24).ToString());
        }
    }

    static string GenerateCompatibilityInfo(string partType, Faker faker)
    {
        return faker.PickRandom(new[] {
            "Compatible with {0} series equipment manufactured after {1}.",
            "Can replace legacy part numbers: {2}, {3}, and {4}.",
            "Not compatible with {5} model due to design changes.",
            "Interchangeable with OEM part number {6} with no modifications.",
            "Fits all {7} models except those with {8} option.",
            "Requires adapter kit when used with older {9} equipment."
        }).Replace("{0}", faker.Commerce.Product())
          .Replace("{1}", faker.Random.Int(1990, 2020).ToString())
          .Replace("{2}", "P" + faker.Random.String2(1, "ABCDEFGHIJKLMNOPQRSTUVWXYZ") + "-" + faker.Random.Number(1000, 9999))
          .Replace("{3}", "P" + faker.Random.String2(1, "ABCDEFGHIJKLMNOPQRSTUVWXYZ") + "-" + faker.Random.Number(1000, 9999))
          .Replace("{4}", "P" + faker.Random.String2(1, "ABCDEFGHIJKLMNOPQRSTUVWXYZ") + "-" + faker.Random.Number(1000, 9999))
          .Replace("{5}", faker.Company.CompanyName())
          .Replace("{6}", faker.Random.String2(2, "ABCDEFGHIJKLMNOPQRSTUVWXYZ") + faker.Random.Number(1000, 9999))
          .Replace("{7}", faker.Commerce.ProductName())
          .Replace("{8}", faker.Commerce.ProductAdjective())
          .Replace("{9}", faker.Company.CompanyName());
    }

    static string GenerateTroubleshootingTip(string partType, Faker faker)
    {
        if (partType.Contains("Valve"))
        {
            return faker.PickRandom(new[] {
                "If valve fails to open/close completely, check actuator air supply pressure.",
                "Leakage through valve seat may indicate erosion or deposits requiring cleaning or replacement.",
                "Excessive shaft leakage indicates packing gland adjustment or replacement is needed.",
                "Jerky operation may be caused by inadequate stem lubrication or misalignment.",
                "Unusual noise during operation often indicates cavitation or flashing; review application parameters.",
                "If valve position does not match controller output, check positioner calibration.",
                "Excessive vibration may indicate improper support or flow-induced turbulence."
            });
        }
        else if (partType.Contains("Pump"))
        {
            return faker.PickRandom(new[] {
                "Low flow rate often indicates clogged suction strainer or worn impeller.",
                "Excessive noise or vibration may be caused by cavitation, misalignment, or bearing failure.",
                "Overheating motor suggests overload, low voltage, or insufficient cooling.",
                "Mechanical seal leakage above acceptable limits requires seal replacement.",
                "Pump fails to prime when air cannot be displaced; check for air leaks in suction line.",
                "Increased power consumption may indicate increased system pressure or mechanical issues.",
                "Erratic flow can be caused by air entrainment or varying suction conditions."
            });
        }
        else if (partType.Contains("Bearing"))
        {
            return faker.PickRandom(new[] {
                "Abnormal noise (clicking, rumbling, squealing) indicates specific bearing damage patterns.",
                "Overheating may be caused by inadequate lubrication, overload, or excessive preload.",
                "Blue discoloration suggests overheating has occurred, requiring immediate replacement.",
                "Premature failure is often caused by improper mounting, contamination, or inadequate lubrication.",
                "Axial movement beyond specifications may indicate bearing inner race slippage.",
                "Fretting corrosion appears as rust-colored powder and indicates relative movement between parts.",
                "Pitting or flaking on rolling surfaces indicates fatigue and end of useful life."
            });
        }
        else
        {
            return faker.PickRandom(new[] {
                "Unexpected failures often result from operating outside design parameters.",
                "Gradual performance degradation typically indicates normal wear rather than specific failure.",
                "Intermittent issues are often related to temperature variations or load fluctuations.",
                "Visual inspection can reveal most external signs of impending failure.",
                "Always verify proper installation before investigating other failure causes.",
                "Document symptom patterns to help identify root causes of recurring issues.",
                "Most failures progress from minor to major; early detection prevents consequential damage."
            });
        }
    }
    #endregion


    // Database methods
    static async Task BulkInsertAsync<T>(string tableName, List<T> items)
    {
        if (items == null || items.Count == 0)
            return;

        Console.WriteLine($"Inserting {items.Count} items into {tableName}...");

        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.OpenAsync();

            // Opret en DataTable fra objekterne
            var dataTable = CreateDataTable(items);

            using (var bulkCopy = new SqlBulkCopy(connection))
            {
                bulkCopy.DestinationTableName = tableName;
                bulkCopy.BatchSize = 10000; // Optimal batchstørrelse
                bulkCopy.BulkCopyTimeout = 300; // 5 minutter timeout

                // Konfigurerer kolonnemapping
                foreach (DataColumn column in dataTable.Columns)
                {
                    bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                }

                await bulkCopy.WriteToServerAsync(dataTable);
            }

            Console.WriteLine($"Inserted {items.Count} records into {tableName}");
        }
    }

    static DataTable CreateDataTable<T>(IEnumerable<T> items)
    {
        var dataTable = new DataTable();
        var properties = typeof(T).GetProperties();

        // Opret kolonner
        foreach (var property in properties)
        {
            Type propertyType = property.PropertyType;

            // Håndter nullable typer
            if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                propertyType = Nullable.GetUnderlyingType(propertyType);
            }

            dataTable.Columns.Add(property.Name, propertyType ?? typeof(object));
        }

        // Fyld rækker med data
        foreach (var item in items)
        {
            var row = dataTable.NewRow();

            foreach (var property in properties)
            {
                var value = property.GetValue(item);
                row[property.Name] = value ?? DBNull.Value;
            }

            dataTable.Rows.Add(row);
        }

        return dataTable;
    }

    // Hjælpefunktioner til datogenerering
    static DateTime GenerateRandomDate(Faker faker, DateTime minDate, DateTime maxDate)
    {
        TimeSpan range = maxDate - minDate;
        TimeSpan randomTime = new TimeSpan(faker.Random.Long(0, range.Ticks));
        return minDate + randomTime;
    }

    static DateTime? GenerateNullableRandomDate(Faker faker, DateTime minDate, DateTime maxDate, float nullProbability = 0.5f)
    {
        if (faker.Random.Float(0, 1) < nullProbability)
        {
            return null;
        }
        return GenerateRandomDate(faker, minDate, maxDate);
    }

    static async Task CreateDatabaseAndTablesAsync()
    {
        Console.WriteLine("Setting up database...");

        using (var connection = new SqlConnection("Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;"))
        {
            await connection.OpenAsync();

            // Tjek om databasen eksisterer
            var checkDbCommand = new SqlCommand(
                "SELECT COUNT(*) FROM sys.databases WHERE name = 'SparePartsDB'",
                connection);
            int dbExists = (int)await checkDbCommand.ExecuteScalarAsync();

            // Slet databasen hvis den eksisterer
            if (dbExists > 0)
            {
                Console.WriteLine("Database SparePartsDB already exists. Attempting to delete...");

                // Sæt databasen i single-user mode for at lukke åbne forbindelser
                var singleUserCommand = new SqlCommand(
                    "ALTER DATABASE SparePartsDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE",
                    connection);
                try
                {
                    await singleUserCommand.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error setting database to single-user mode: {ex.Message}");
                    Console.WriteLine("Please ensure no other connections are open to SparePartsDB.");
                    return; // Afbryd hvis vi ikke kan få eksklusiv adgang
                }

                // Slet databasen
                var dropDbCommand = new SqlCommand(
                    "DROP DATABASE SparePartsDB",
                    connection);
                await dropDbCommand.ExecuteNonQueryAsync();
                Console.WriteLine("Database SparePartsDB deleted successfully.");
            }

            // Opret databasen
            Console.WriteLine("Creating database SparePartsDB...");
            var createDbCommand = new SqlCommand(
                "CREATE DATABASE SparePartsDB",
                connection);
            await createDbCommand.ExecuteNonQueryAsync();
            Console.WriteLine("Database SparePartsDB created successfully.");
        }

        // Opret tabeller i databasen
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.OpenAsync();

            Console.WriteLine("Creating tables...");

            // Disable constraints for faster bulk inserts
            var disableConstraintsCommand = new SqlCommand(
                "EXEC sp_MSforeachtable \"ALTER TABLE ? NOCHECK CONSTRAINT all\"",
                connection);

            await disableConstraintsCommand.ExecuteNonQueryAsync();

            // Opret Unit tabel
            await ExecuteNonQueryAsync(ConnectionString, @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Unit')
            CREATE TABLE Unit (
                Id UNIQUEIDENTIFIER PRIMARY KEY,
                UnitNo NVARCHAR(50) NOT NULL,
                Name NVARCHAR(100) NOT NULL,
                Description NVARCHAR(500) NULL,
                IsActive BIT NOT NULL DEFAULT 1
            )
        ");

            // Opret Manufacturer tabel
            await ExecuteNonQueryAsync(ConnectionString, @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Manufacturer')
            CREATE TABLE Manufacturer (
                Id UNIQUEIDENTIFIER PRIMARY KEY,
                master_id UNIQUEIDENTIFIER NULL,
                UnitGuid UNIQUEIDENTIFIER NOT NULL,
                ManufacturerNo NVARCHAR(50) NOT NULL,
                Name NVARCHAR(100) NULL,
                Country NVARCHAR(50) NULL,
                Notes NVARCHAR(500) NULL,
                CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
                LastModifiedDate DATETIME NULL,
                CONSTRAINT FK_Manufacturer_Unit FOREIGN KEY (UnitGuid) REFERENCES Unit(Id),
                CONSTRAINT FK_Manufacturer_Master FOREIGN KEY (master_id) REFERENCES Manufacturer(Id)
            )
        ");

            // Opret Category tabel
            await ExecuteNonQueryAsync(ConnectionString, @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Category')
            CREATE TABLE Category (
                Id UNIQUEIDENTIFIER PRIMARY KEY,
                master_id UNIQUEIDENTIFIER NULL,
                UnitGuid UNIQUEIDENTIFIER NOT NULL,
                CategoryNo NVARCHAR(50) NOT NULL,
                Name NVARCHAR(100) NULL,
                Description NVARCHAR(500) NULL,
                CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
                LastModifiedDate DATETIME NULL,
                CONSTRAINT FK_Category_Unit FOREIGN KEY (UnitGuid) REFERENCES Unit(Id),
                CONSTRAINT FK_Category_Master FOREIGN KEY (master_id) REFERENCES Category(Id)
            )
        ");

            // Opret Supplier tabel
            await ExecuteNonQueryAsync(ConnectionString, @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Supplier')
            CREATE TABLE Supplier (
                Id UNIQUEIDENTIFIER PRIMARY KEY,
                master_id UNIQUEIDENTIFIER NULL,
                UnitGuid UNIQUEIDENTIFIER NOT NULL,
                SupplierNo NVARCHAR(50) NOT NULL,
                Name NVARCHAR(100) NULL,
                ContactInfo NVARCHAR(200) NULL,
                Notes NVARCHAR(500) NULL,
                CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
                LastModifiedDate DATETIME NULL,
                CONSTRAINT FK_Supplier_Unit FOREIGN KEY (UnitGuid) REFERENCES Unit(Id),
                CONSTRAINT FK_Supplier_Master FOREIGN KEY (master_id) REFERENCES Supplier(Id)
            )
        ");

            // Opret Location tabel
            await ExecuteNonQueryAsync(ConnectionString, @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Location')
            CREATE TABLE Location (
                Id UNIQUEIDENTIFIER PRIMARY KEY,
                master_id UNIQUEIDENTIFIER NULL,
                UnitGuid UNIQUEIDENTIFIER NOT NULL,
                LocationNo NVARCHAR(50) NOT NULL,
                Name NVARCHAR(100) NULL,
                Area NVARCHAR(100) NULL,
                Building NVARCHAR(100) NULL,
                Notes NVARCHAR(500) NULL,
                CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
                LastModifiedDate DATETIME NULL,
                CONSTRAINT FK_Location_Unit FOREIGN KEY (UnitGuid) REFERENCES Unit(Id),
                CONSTRAINT FK_Location_Master FOREIGN KEY (master_id) REFERENCES Location(Id)
            )
        ");

            // Opret Component tabel
            await ExecuteNonQueryAsync(ConnectionString, @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Component')
            CREATE TABLE Component (
                Id UNIQUEIDENTIFIER PRIMARY KEY,
                master_id UNIQUEIDENTIFIER NULL,
                UnitGuid UNIQUEIDENTIFIER NOT NULL,
                ComponentNo NVARCHAR(50) NOT NULL,
                Code NVARCHAR(100) NOT NULL,
                Level INT NOT NULL,
                ParentComponentGuid UNIQUEIDENTIFIER NULL,
                Name NVARCHAR(100) NULL,
                Description NVARCHAR(500) NULL,
                CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
                LastModifiedDate DATETIME NULL,
                CONSTRAINT FK_Component_Unit FOREIGN KEY (UnitGuid) REFERENCES Unit(Id),
                CONSTRAINT FK_Component_Parent FOREIGN KEY (ParentComponentGuid) REFERENCES Component(Id),
                CONSTRAINT FK_Component_Master FOREIGN KEY (master_id) REFERENCES Component(Id)
            )
        ");

            // Opret SparePart tabel
            await ExecuteNonQueryAsync(ConnectionString, $@"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SparePart')
            CREATE TABLE SparePart (
                Id UNIQUEIDENTIFIER PRIMARY KEY,
                master_id UNIQUEIDENTIFIER NULL,
                UnitGuid UNIQUEIDENTIFIER NOT NULL,
                SparePartNo NVARCHAR(50) NOT NULL,
                SparePartSerialCode NVARCHAR(50) NOT NULL,
                Name NVARCHAR(100) NULL,
                Description NVARCHAR(2000) NULL,
                ManufacturerGuid UNIQUEIDENTIFIER NULL,
                CategoryGuid UNIQUEIDENTIFIER NULL,
                SupplierGuid UNIQUEIDENTIFIER NULL,
                LocationGuid UNIQUEIDENTIFIER NULL,
                TypeNo NVARCHAR(50) NULL,
                Notes NVARCHAR(500) NULL,
                MeasurementUnit NVARCHAR(20) NULL,
                CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
                LastModifiedDate DATETIME NULL,
                BasePrice DECIMAL(18,2) NULL,
                Currency NVARCHAR(10) NULL,
                PriceDate DATETIME NULL,
                CONSTRAINT FK_SparePart_Unit FOREIGN KEY (UnitGuid) REFERENCES Unit(Id),
                CONSTRAINT FK_SparePart_Manufacturer FOREIGN KEY (ManufacturerGuid) REFERENCES Manufacturer(Id),
                CONSTRAINT FK_SparePart_Category FOREIGN KEY (CategoryGuid) REFERENCES Category(Id),
                CONSTRAINT FK_SparePart_Supplier FOREIGN KEY (SupplierGuid) REFERENCES Supplier(Id),
                CONSTRAINT FK_SparePart_Location FOREIGN KEY (LocationGuid) REFERENCES Location(Id),
                CONSTRAINT FK_SparePart_Master FOREIGN KEY (master_id) REFERENCES SparePart(Id)
            )
        ");

            // Opret ComponentPart tabel
            await ExecuteNonQueryAsync(ConnectionString, @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ComponentPart')
            CREATE TABLE ComponentPart (
                Id UNIQUEIDENTIFIER PRIMARY KEY,
                master_id UNIQUEIDENTIFIER NULL,
                UnitGuid UNIQUEIDENTIFIER NOT NULL,
                ComponentGuid UNIQUEIDENTIFIER NOT NULL,
                SparePartGuid UNIQUEIDENTIFIER NOT NULL,
                Quantity INT NOT NULL DEFAULT 1,
                Position NVARCHAR(50) NULL,
                Notes NVARCHAR(200) NULL,
                CreatedDate DATETIME NOT NULL DEFAULT GETDATE(),
                LastModifiedDate DATETIME NULL,
                CONSTRAINT FK_ComponentPart_Unit FOREIGN KEY (UnitGuid) REFERENCES Unit(Id),
                CONSTRAINT FK_ComponentPart_Component FOREIGN KEY (ComponentGuid) REFERENCES Component(Id),
                CONSTRAINT FK_ComponentPart_SparePart FOREIGN KEY (SparePartGuid) REFERENCES SparePart(Id),
                CONSTRAINT FK_ComponentPart_Master FOREIGN KEY (master_id) REFERENCES ComponentPart(Id)
            )
        ");
            Console.WriteLine("Database tables created successfully.");
        }

        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            Console.WriteLine("Preparing to update indexes...");

            // 1. Identificer og gem alle FK-constraints for senere genskabelse
            var getAllForeignKeysCmd = new SqlCommand(@"
            SELECT 
                OBJECT_NAME(f.parent_object_id) AS TableName,
                f.name AS ForeignKeyName,
                OBJECT_NAME(f.referenced_object_id) AS ReferencedTableName,
                COL_NAME(fc.parent_object_id, fc.parent_column_id) AS ColumnName,
                COL_NAME(fc.referenced_object_id, fc.referenced_column_id) AS ReferencedColumnName
            FROM 
                sys.foreign_keys f
                INNER JOIN sys.foreign_key_columns fc ON f.OBJECT_ID = fc.constraint_object_id
            WHERE 
                OBJECT_NAME(f.parent_object_id) IN 
                ('Manufacturer', 'Category', 'Supplier', 'Location', 'Component', 'SparePart', 'ComponentPart')
            ORDER BY 
                OBJECT_NAME(f.parent_object_id)", connection);

            var fkConstraints = new List<(string TableName, string FkName, string RefTableName, string ColumnName, string RefColumnName)>();
            using (var reader = await getAllForeignKeysCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    fkConstraints.Add((
                        reader["TableName"].ToString(),
                        reader["ForeignKeyName"].ToString(),
                        reader["ReferencedTableName"].ToString(),
                        reader["ColumnName"].ToString(),
                        reader["ReferencedColumnName"].ToString()
                    ));
                }
            }

            Console.WriteLine($"Found {fkConstraints.Count} foreign key constraints to manage");

            // 2. Drop alle foreign key constraints
            foreach (var (tableName, fkName, _, _, _) in fkConstraints)
            {
                try
                {
                    Console.WriteLine($"Dropping foreign key {fkName} on {tableName}");
                    var dropFkCmd = new SqlCommand($"ALTER TABLE {tableName} DROP CONSTRAINT {fkName}", connection);
                    await dropFkCmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error dropping FK {fkName}: {ex.Message}");
                }
            }

            // 3. Identificer og konverter alle primary key constraints til nonclustered
            var tables = new[] { "Manufacturer", "Category", "Supplier", "Location", "Component", "SparePart", "ComponentPart" };

            var pkConstraints = new List<(string TableName, string PkName, string ColumnName)>();

            foreach (var table in tables)
            {
                // Find primary key
                var findPkCmd = new SqlCommand(
                    $@"SELECT i.name AS IndexName, COL_NAME(ic.object_id, ic.column_id) AS ColumnName
                   FROM sys.indexes i
                   JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                   WHERE i.object_id = OBJECT_ID('{table}')
                   AND i.is_primary_key = 1", connection);

                using (var reader = await findPkCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        pkConstraints.Add((
                            table,
                            reader["IndexName"].ToString(),
                            reader["ColumnName"].ToString()
                        ));
                    }
                }
            }

            // Drop de eksisterende primary keys
            foreach (var (tableName, pkName, _) in pkConstraints)
            {
                try
                {
                    Console.WriteLine($"Dropping primary key {pkName} on {tableName}");
                    var dropPkCmd = new SqlCommand($"ALTER TABLE {tableName} DROP CONSTRAINT {pkName}", connection);
                    await dropPkCmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error dropping PK {pkName}: {ex.Message}");
                }
            }

            // 4. Opret clustered indexes på UnitGuid og master_id
            Console.WriteLine("Creating clustered indexes...");

            var clusteredIndexCommands = new[]
            {
            "CREATE CLUSTERED INDEX CIX_Manufacturer_UnitGuid_master_id ON Manufacturer(UnitGuid, master_id)",
            "CREATE CLUSTERED INDEX CIX_Category_UnitGuid_master_id ON Category(UnitGuid, master_id)",
            "CREATE CLUSTERED INDEX CIX_Supplier_UnitGuid_master_id ON Supplier(UnitGuid, master_id)",
            "CREATE CLUSTERED INDEX CIX_Location_UnitGuid_master_id ON Location(UnitGuid, master_id)",
            "CREATE CLUSTERED INDEX CIX_Component_UnitGuid_master_id ON Component(UnitGuid, master_id)",
            "CREATE CLUSTERED INDEX CIX_SparePart_UnitGuid_master_id ON SparePart(UnitGuid, master_id)",
            "CREATE CLUSTERED INDEX CIX_ComponentPart_UnitGuid_master_id ON ComponentPart(UnitGuid, master_id)",
        };

            foreach (var cmdText in clusteredIndexCommands)
            {
                try
                {
                    var cmd = new SqlCommand(cmdText, connection);
                    await cmd.ExecuteNonQueryAsync();
                    Console.WriteLine($"Created: {cmdText}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating index: {cmdText} - {ex.Message}");
                }
            }

            // 5. Genopret primary keys som nonclustered
            foreach (var (tableName, pkName, columnName) in pkConstraints)
            {
                try
                {
                    Console.WriteLine($"Recreating primary key {pkName} on {tableName} as nonclustered");
                    var createPkCmd = new SqlCommand(
                        $"ALTER TABLE {tableName} ADD CONSTRAINT {pkName} PRIMARY KEY NONCLUSTERED ({columnName})",
                        connection);
                    await createPkCmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error recreating PK {pkName}: {ex.Message}");
                }
            }

            // 6. Genopret alle foreign key constraints
            foreach (var (tableName, fkName, refTableName, columnName, refColumnName) in fkConstraints)
            {
                try
                {
                    Console.WriteLine($"Recreating foreign key {fkName} on {tableName}");
                    var createFkCmd = new SqlCommand(
                        $"ALTER TABLE {tableName} ADD CONSTRAINT {fkName} " +
                        $"FOREIGN KEY ({columnName}) REFERENCES {refTableName}({refColumnName})",
                        connection);
                    await createFkCmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error recreating FK {fkName}: {ex.Message}");
                }
            }

            Console.WriteLine("Database schema and indexes created successfully.");
        }
    }

    static async Task CreateFullTextIndexesAsync()
    {
        Console.WriteLine("Creating Full-Text indexes...");

        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.OpenAsync();

            // Tjek om full-text catalog eksisterer, hvis ikke, opret det
            var checkCatalogCmd = new SqlCommand(
                "IF NOT EXISTS (SELECT * FROM sys.fulltext_catalogs WHERE name = 'SparePartsCatalog') " +
                "CREATE FULLTEXT CATALOG SparePartsCatalog AS DEFAULT",
                connection);
            await checkCatalogCmd.ExecuteNonQueryAsync();

            // Funktion til at oprette full-text indeks for en given tabel
            async Task CreateFullTextIndexForTableAsync(string tableName, string[] columns)
            {
                var getPrimaryKeyIndexNameCmd = new SqlCommand(
                    $"SELECT i.name FROM sys.indexes i JOIN sys.objects o ON i.object_id = o.object_id WHERE o.type = 'U' AND o.name = '{tableName}' AND i.is_primary_key = 1",
                    connection
                );
                var primaryKeyIndexName = (string?)await getPrimaryKeyIndexNameCmd.ExecuteScalarAsync();

                if (!string.IsNullOrEmpty(primaryKeyIndexName))
                {
                    var columnsClause = string.Join(", ", columns.Select(c => $"{c} LANGUAGE 1033"));
                    var createIndexCommandText = $@"
                        IF NOT EXISTS (SELECT * FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('{tableName}'))
                        BEGIN
                            CREATE FULLTEXT INDEX ON {tableName} (
                                {columnsClause}
                            ) KEY INDEX {primaryKeyIndexName} ON SparePartsCatalog WITH CHANGE_TRACKING AUTO;
                        END";
                    await ExecuteNonQueryAsync(ConnectionString, createIndexCommandText);
                    Console.WriteLine($"Full-Text index created on {tableName}.");
                }
                else
                {
                    Console.WriteLine($"Warning: Could not find primary key index for table {tableName}. Full-Text index not created.");
                }
            }

            // Opret full-text indeks på de relevante tabeller
            await CreateFullTextIndexForTableAsync("SparePart", new[] { "Name", "Description", "Notes", "SparePartNo", "SparePartSerialCode", "TypeNo" });
            await CreateFullTextIndexForTableAsync("Component", new[] { "Name", "Description", "ComponentNo", "Code" });
            await CreateFullTextIndexForTableAsync("Manufacturer", new[] { "Name", "Notes", "ManufacturerNo" });
            await CreateFullTextIndexForTableAsync("Category", new[] { "Name", "Description", "CategoryNo" });
            await CreateFullTextIndexForTableAsync("Supplier", new[] { "Name", "ContactInfo", "Notes", "SupplierNo" });
            await CreateFullTextIndexForTableAsync("Location", new[] { "Name", "Area", "Building", "Notes", "LocationNo" });
            await CreateFullTextIndexForTableAsync("ComponentPart", new[] { "Position", "Notes" });

            Console.WriteLine("Full-Text indexes created successfully.");
        }
    }

    static async Task CreateIndexesAsync()
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            Console.WriteLine("Creating indexes...");

            var indexCommands = new[]
            {
            // Unit
            "CREATE INDEX IX_Unit_UnitNo ON Unit(UnitNo)",
            "CREATE INDEX IX_Unit_Name ON Unit(Name)",

            // Manufacturer
            "CREATE INDEX IX_Manufacturer_ManufacturerNo ON Manufacturer(ManufacturerNo)",
            "CREATE INDEX IX_Manufacturer_Name ON Manufacturer(Name)",
            "CREATE INDEX IX_Manufacturer_UnitGuid ON Manufacturer(UnitGuid)",

            // Category
            "CREATE INDEX IX_Category_CategoryNo ON Category(CategoryNo)",
            "CREATE INDEX IX_Category_Name ON Category(Name)",
            "CREATE INDEX IX_Category_UnitGuid ON Category(UnitGuid)",

            // Supplier
            "CREATE INDEX IX_Supplier_SupplierNo ON Supplier(SupplierNo)",
            "CREATE INDEX IX_Supplier_Name ON Supplier(Name)",
            "CREATE INDEX IX_Supplier_UnitGuid ON Supplier(UnitGuid)",

            // Location
            "CREATE INDEX IX_Location_LocationNo ON Location(LocationNo)",
            "CREATE INDEX IX_Location_Name ON Location(Name)",
            "CREATE INDEX IX_Location_UnitGuid ON Location(UnitGuid)",

            // Component
            "CREATE INDEX IX_Component_ComponentNo ON Component(ComponentNo)",
            "CREATE INDEX IX_Component_Code ON Component(Code)",
            "CREATE INDEX IX_Component_Name ON Component(Name)",
            "CREATE INDEX IX_Component_UnitGuid ON Component(UnitGuid)",

            // SparePart
            "CREATE INDEX IX_SparePart_SparePartNo ON SparePart(SparePartNo)",
            "CREATE INDEX IX_SparePart_SerialCode ON SparePart(SparePartSerialCode)",
            "CREATE INDEX IX_SparePart_Name ON SparePart(Name)",
            "CREATE INDEX IX_SparePart_UnitGuid ON SparePart(UnitGuid)",

            // ComponentPart
            "CREATE INDEX IX_ComponentPart_ComponentGuid ON ComponentPart(ComponentGuid)",
            "CREATE INDEX IX_ComponentPart_SparePartGuid ON ComponentPart(SparePartGuid)",
            "CREATE INDEX IX_ComponentPart_UnitGuid ON ComponentPart(UnitGuid)",
        };

            // TODO: Add "History_Tunining" indexes ... We need IDSeq column - global tabel sequence decending count

            foreach (var cmdText in indexCommands)
            {
                try
                {
                    var cmd = new SqlCommand(cmdText, connection);
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (SqlException ex) when (ex.Number == 1913) // index already exists
                {
                    Console.WriteLine($"Index already exists: {cmdText}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating index: {cmdText} - {ex.Message}");
                }
            }

            Console.WriteLine("Indexes for tables created successfully.");
        }
    }


    static async Task DisableConstraintsAndIndexesAsync()
    {
        Console.WriteLine("Disabling constraints and indexes for faster data insertion...");

        // Deaktiver alle constraints
        await ExecuteNonQueryAsync(ConnectionString,
            "EXEC sp_MSforeachtable \"ALTER TABLE ? NOCHECK CONSTRAINT all\"");

        // Deaktiver alle non-clustered indekser
        await ExecuteNonQueryAsync(ConnectionString, @"
        DECLARE @sql NVARCHAR(MAX) = N'';
        SELECT @sql += N'ALTER INDEX ' + QUOTENAME(i.name) + 
                        N' ON ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + 
                        N'.' + QUOTENAME(t.name) + N' DISABLE;' + CHAR(13) + CHAR(10)
        FROM sys.indexes i
        JOIN sys.tables t ON i.object_id = t.object_id
        WHERE i.type_desc = 'NONCLUSTERED' 
        AND i.is_primary_key = 0
        AND i.is_unique_constraint = 0
        AND t.is_ms_shipped = 0;

        EXEC sp_executesql @sql;
        ");
    }

    // Hjælpefunktion til at genaktivere begrænsninger og indekser efter bulk insert
    static async Task EnableConstraintsAndIndexesAsync()
    {
        Console.WriteLine("Re-enabling constraints and indexes...");

        // Genaktiver alle non-clustered indekser
        await ExecuteNonQueryAsync(ConnectionString, @"
        DECLARE @sql NVARCHAR(MAX) = N'';
        SELECT @sql += N'ALTER INDEX ' + QUOTENAME(i.name) + 
                        N' ON ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + 
                        N'.' + QUOTENAME(t.name) + N' REBUILD;' + CHAR(13) + CHAR(10)
        FROM sys.indexes i
        JOIN sys.tables t ON i.object_id = t.object_id
        WHERE i.type_desc = 'NONCLUSTERED' 
        AND i.is_primary_key = 0
        AND i.is_unique_constraint = 0
        AND t.is_ms_shipped = 0;

        EXEC sp_executesql @sql;
        ");

        // Genaktiver alle constraints
        await ExecuteNonQueryAsync(ConnectionString,
            "EXEC sp_MSforeachtable \"ALTER TABLE ? WITH CHECK CHECK CONSTRAINT all\"");
       
    }

    static async Task<double> GetDatabaseSizeInGBAsync()
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.OpenAsync();

            var command = new SqlCommand(@"
            SELECT SUM(size * 8.0 / 1024 / 1024) AS SizeInGB
            FROM sys.master_files
            WHERE database_id = DB_ID('SparePartsDB')
            GROUP BY database_id", connection);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToDouble(result);
        }
    }

    // Hjælpemetode til at køre SQL-kommandoer
    static async Task ExecuteNonQueryAsync(string databaseName, string commandText)
    {
        string connectionString = databaseName == "master" ? "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;" : ConnectionString;
        using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            using (var command = new SqlCommand(commandText, connection))
            {
                await command.ExecuteNonQueryAsync();
            }
        }
    }
}

// Model classes matching the database
// Note: SparePart class has been moved up inside the TestDataGenerator class with the additional fields

public class SparePart
{
    public Guid Id { get; set; }
    public Guid? master_id { get; set; }
    public Guid UnitGuid { get; set; }
    public string SparePartNo { get; set; }
    public string SparePartSerialCode { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public Guid? ManufacturerGuid { get; set; }
    public Guid? CategoryGuid { get; set; }
    public Guid? SupplierGuid { get; set; }
    public Guid? LocationGuid { get; set; }
    public string TypeNo { get; set; }
    public string Notes { get; set; }

    // Additional fields for improved test data
    public string MeasurementUnit { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public decimal? BasePrice { get; set; }
    public string Currency { get; set; }
    public DateTime? PriceDate { get; set; }
}

public class Unit
{
    public Guid Id { get; set; }
    public string UnitNo { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; }
}

public class Component
{
    public Guid Id { get; set; }
    public Guid? master_id { get; set; }
    public Guid UnitGuid { get; set; }
    public string ComponentNo { get; set; }
    public string Code { get; set; }
    public int Level { get; set; }
    public Guid? ParentComponentGuid { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }

    // Additional fields for improved test data
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}

public class ComponentPart
{
    public Guid Id { get; set; }
    public Guid? master_id { get; set; }
    public Guid UnitGuid { get; set; }
    public Guid ComponentGuid { get; set; }
    public Guid SparePartGuid { get; set; }
    public int Quantity { get; set; }
    public string Position { get; set; }
    public string Notes { get; set; }

    // Additional fields for improved test data
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}

public class Manufacturer
{
    public Guid Id { get; set; }
    public Guid? master_id { get; set; }
    public Guid UnitGuid { get; set; }
    public string ManufacturerNo { get; set; }
    public string Name { get; set; }
    public string Country { get; set; }
    public string Notes { get; set; }

    // Additional fields for improved test data
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}

public class Category
{
    public Guid Id { get; set; }
    public Guid? master_id { get; set; }
    public Guid UnitGuid { get; set; }
    public string CategoryNo { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }

    // Additional fields for improved test data
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}

public class Supplier
{
    public Guid Id { get; set; }
    public Guid? master_id { get; set; }
    public Guid UnitGuid { get; set; }
    public string SupplierNo { get; set; }
    public string Name { get; set; }
    public string ContactInfo { get; set; }
    public string Notes { get; set; }

    // Additional fields for improved test data
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}

public class Location
{
    public Guid Id { get; set; }
    public Guid? master_id { get; set; }
    public Guid UnitGuid { get; set; }
    public string LocationNo { get; set; }
    public string Name { get; set; }
    public string Area { get; set; }
    public string Building { get; set; }
    public string Notes { get; set; }

    // Additional fields for improved test data
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
}