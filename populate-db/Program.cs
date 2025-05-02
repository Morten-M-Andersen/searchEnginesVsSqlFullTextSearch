using Bogus;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

class TestDataGenerator
{
    private const string ConnectionString = "Server=localhost;Database=SparePartsDB;Trusted_Connection=True;";

    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting test data generation...");

        // Definer antal records per type
        int unitCount = 5;
        int manufacturerCountPerUnit = 20;
        int categoryCountPerUnit = 30;
        int supplierCountPerUnit = 15;
        int locationCountPerUnit = 20;
        int componentCountPerUnitActive = 2300;
        int componentCountPerUnitHistoric = 8700;
        int sparePartCountPerUnitActive = 134000;
        int sparePartCountPerUnitHistoric = 527000;

        // Initier Faker med dansk lokalisering
        var faker = new Faker("da");

        // 1. Generer Units
        var units = GenerateUnits(unitCount, faker);
        await BulkInsertAsync("Unit", units);
        Console.WriteLine($"Generated {units.Count} units");

        // Generer data for hver unit
        foreach (var unit in units)
        {
            Console.WriteLine($"Generating data for unit: {unit.Name} ({unit.UnitNo})");

            // 2. Generer Manufacturers for denne unit
            var manufacturers = GenerateManufacturers(manufacturerCountPerUnit, unit.Id, faker);
            await BulkInsertAsync("Manufacturer", manufacturers);

            // 3. Generer Categories for denne unit
            var categories = GenerateCategories(categoryCountPerUnit, unit.Id, faker);
            await BulkInsertAsync("Category", categories);

            // 4. Generer Suppliers for denne unit
            var suppliers = GenerateSuppliers(supplierCountPerUnit, unit.Id, faker);
            await BulkInsertAsync("Supplier", suppliers);

            // 5. Generer Locations for denne unit
            var locations = GenerateLocations(locationCountPerUnit, unit.Id, faker);
            await BulkInsertAsync("Location", locations);

            // 6. Generer Components (aktive) for denne unit
            var activeComponents = GenerateComponents(
                componentCountPerUnitActive,
                null,
                unit.Id,
                faker,
                categories.Select(c => c.Id).ToList()
            );
            await BulkInsertAsync("Component", activeComponents);

            // 7. Generer Components (historiske) for denne unit
            var historicComponents = GenerateHistoricalComponents(
                componentCountPerUnitHistoric,
                activeComponents,
                unit.Id,
                faker
            );
            await BulkInsertAsync("Component", historicComponents);

            // 8. Generer SpareParts (aktive) for denne unit
            var activeSpareParts = GenerateSpareParts(
                sparePartCountPerUnitActive,
                null,
                unit.Id,
                manufacturers.Select(m => m.Id).ToList(),
                categories.Select(c => c.Id).ToList(),
                suppliers.Select(s => s.Id).ToList(),
                locations.Select(l => l.Id).ToList(),
                faker
            );
            await BulkInsertAsync("SparePart", activeSpareParts);

            // 9. Generer SpareParts (historiske) for denne unit
            var historicSpareParts = GenerateHistoricalSpareParts(
                sparePartCountPerUnitHistoric,
                activeSpareParts,
                unit.Id,
                manufacturers.Select(m => m.Id).ToList(),
                categories.Select(c => c.Id).ToList(),
                suppliers.Select(s => s.Id).ToList(),
                locations.Select(l => l.Id).ToList(),
                faker
            );
            await BulkInsertAsync("SparePart", historicSpareParts);

            // 10. Generer ComponentPart relationer
            var componentParts = GenerateComponentParts(
                activeComponents.Concat(historicComponents).ToList(),
                activeSpareParts.Concat(historicSpareParts).ToList(),
                unit.Id,
                faker
            );
            await BulkInsertAsync("ComponentPart", componentParts);
        }

        Console.WriteLine("Test data generation completed successfully!");
    }

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

    // Liste af typiske søgeord og fraser for reservedele
    private static readonly List<string> CommonSearchTerms = new List<string> {
        "overhaul kit", "repair kit", "seal kit", "gasket set", "bearing assembly",
        "replacement filter", "high performance", "high temperature", "corrosion resistant",
        "heavy duty", "emergency spare", "critical part", "OEM equivalent", "recommended spare",
        "preventive maintenance", "outage spare", "long lead time", "offshore rated", "ATEX approved"
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

    // Generer Unit data
    static List<Unit> GenerateUnits(int count, Faker faker)
    {
        var units = new List<Unit>();

        var unitNames = new[] {
            "Marine Division", "Power Generation", "Offshore Platform",
            "Production Plant", "Process Facility"
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
                    Notes = faker.Random.Bool(0.7f) ? GenerateManufacturerNotes(faker) : null
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
                Notes = faker.Random.Bool(0.7f) ? GenerateManufacturerNotes(faker) : null
            });
        }

        return manufacturers;
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
                    Description = $"Main category for {categorySystem.ToLower()} components and parts"
                });

                // Opbevar relation mellem kategorinavn og ID
                categoryRelations[categorySystem] = new List<string>();
                index++;
            }
        }

        // Derefter generer underkategorier til hver hovedkategori
        foreach (var category in ComponentsByCategory)
        {
            string mainCategory = category.Key;
            var subCategories = category.Value;

            // Find ID'et på hovedkategorien
            var mainCategoryObj = categories.FirstOrDefault(c => c.Name == mainCategory);
            if (mainCategoryObj != null)
            {
                // Tilføj underkategorier
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
                            Description = $"Subcategory for {subCategory.ToLower()} within {mainCategory.ToLower()} systems"
                        });

                        // Tilføj til relations dictionary
                        categoryRelations[mainCategory].Add(subCategory);
                        index++;
                    }
                }
            }
        }

        // Hvis vi stadig mangler kategorier, tilføj flere detaljerede underkategorier
        while (index < count)
        {
            // Vælg en tilfældig eksisterende underkategori som udgangspunkt
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
                    Description = $"Specialized {variant.ToLower()} variant of {existingCat.Name.ToLower()}"
                });
                index++;
            }
            else
            {
                // Hvis ingen underkategorier findes, lav en ny generisk kategori
                var categoryName = $"{faker.Commerce.Department()} Parts";
                categories.Add(new Category
                {
                    Id = Guid.NewGuid(),
                    master_id = null,
                    UnitGuid = unitGuid,
                    CategoryNo = $"CAT-{index + 1:D3}",
                    Name = categoryName,
                    Description = $"General category for {categoryName.ToLower()}"
                });
                index++;
            }
        }

        return categories;
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
                Notes = faker.Random.Bool(0.6f) ? GenerateSupplierNotes(faker, supplierType) : null
            });
        }

        return suppliers;
    }

    // Generer lokationer med realistiske navne og information
    static List<Location> GenerateLocations(int count, Guid unitGuid, Faker faker)
    {
        var locations = new List<Location>();

        // Realistiske områder baseret på industrien
        var areas = new[] {
            "North", "South", "East", "West", "Central",
            "Upper Deck", "Lower Deck", "Main Deck", "Process Area",
            "Production", "Warehouse", "Technical"
        };

        // Realistiske bygningstyper
        var buildings = new[] {
            "Warehouse", "Production Hall", "Main Building", "Workshop",
            "Storage Area", "Engine Room", "Control Room", "Technical Room",
            "Assembly Shop", "Maintenance Building", "Logistics Center"
        };

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
                Notes = faker.Random.Bool(0.4f) ? GenerateLocationNotes(faker) : null
            });
        }

        return locations;
    }

    // Generer komponenter med realistisk kodehierarki
    static List<Component> GenerateComponents(int count, List<Component> baseComponents, Guid unitGuid, Faker faker, List<Guid> categoryIds)
    {
        var components = new List<Component>();

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
                Description = $"Main system for {mainSystems[code].ToLower()} related functions and components"
            };

            components.Add(component);
            level1Components.Add(component);
        }

        // Level 2 komponenter (subsystemer)
        var level2Components = new List<Component>();
        for (int i = 0; i < level2Count; i++)
        {
            // Vælg en tilfældig level 1 komponent som parent
            var parent = faker.PickRandom(level1Components);

            // Generer kode som parentkode.XX
            string subCode = String.Format("{0}.{1:D2}", parent.Code, faker.Random.Number(1, 99));

            // Skab et subsystem-navn baseret på forældrenavnet
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
                Description = GenerateComponentDescription(subsystemName, parent.Name, faker, 2)
            };

            components.Add(component);
            level2Components.Add(component);
        }

        // Level 3 komponenter
        var level3Components = new List<Component>();
        for (int i = 0; i < level3Count; i++)
        {
            // Vælg en tilfældig level 2 komponent som parent
            var parent = faker.PickRandom(level2Components);

            // Generer kode som parentkode.XX
            string subCode = String.Format("{0}.{1:D2}", parent.Code, faker.Random.Number(1, 99));

            // Skab et komponetnavn baseret på forældrenavnet
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
                Description = GenerateComponentDescription(componentName, parent.Name, faker, 3)
            };

            components.Add(component);
            level3Components.Add(component);
        }

        // Level 4 komponenter
        var level4Components = new List<Component>();
        for (int i = 0; i < level4Count; i++)
        {
            // Vælg en tilfældig level 3 komponent som parent
            var parent = faker.PickRandom(level3Components);

            // Generer kode som parentkode.XX
            string subCode = String.Format("{0}.{1:D2}", parent.Code, faker.Random.Number(1, 99));

            // Skab et assemblynavn baseret på forældrenavnet
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
                Description = GenerateComponentDescription(assemblyName, parent.Name, faker, 4)
            };

            components.Add(component);
            level4Components.Add(component);
        }

        // Level 5 komponenter (enkeltdele)
        for (int i = 0; i < level5Count; i++)
        {
            // Vælg en tilfældig level 4 komponent som parent
            var parent = faker.PickRandom(level4Components);

            // Generer kode som parentkode.XX
            string subCode = String.Format("{0}.{1:D2}", parent.Code, faker.Random.Number(1, 99));

            // Skab et delkomponentnavn baseret på forældrenavnet
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
                Description = GenerateComponentDescription(partName, parent.Name, faker, 5)
            };

            components.Add(component);
        }

        return components;
    }

    // Generer historiske versioner af komponenter
    static List<Component> GenerateHistoricalComponents(int count, List<Component> activeComponents, Guid unitGuid, Faker faker)
    {
        var historicalComponents = new List<Component>();

        // For hver aktiv komponent, lav tilfældigt 1-5 historiske versioner
        foreach (var activeComponent in activeComponents)
        {
            int versionCount = faker.Random.Int(1, 5);

            for (int v = 0; v < versionCount && historicalComponents.Count < count; v++)
            {
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
                    Description = $"Historical version {v + 1} of {activeComponent.Name}. {faker.Lorem.Sentence()}"
                };

                historicalComponents.Add(historicalComponent);
            }
        }

        // Hvis vi har genereret for mange, trim listen
        if (historicalComponents.Count > count)
        {
            historicalComponents = historicalComponents.Take(count).ToList();
        }
        // Hvis vi har genereret for få, kopier tilfældige igen
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
                    Description = $"Legacy version of {randomActiveComponent.Name}. {faker.Lorem.Paragraph()}"
                };

                historicalComponents.Add(additionalHistorical);
            }
        }

        return historicalComponents;
    }

    // Generer reservedele
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

        // Hvis der er baseSpareParts, generer varianter baseret på dem (historiske)
        if (baseSpareParts != null && baseSpareParts.Count > 0)
        {
            foreach (var basePart in baseSpareParts)
            {
                // For hver basePart, generer flere versioner (1-3)
                int versionsToGenerate = faker.Random.Int(1, 3);
                for (int i = 0; i < versionsToGenerate && spareParts.Count < count; i++)
                {
                    spareParts.Add(new SparePart
                    {
                        Id = Guid.NewGuid(),
                        master_id = basePart.Id,
                        UnitGuid = unitGuid,
                        SparePartNo = GenerateRevisionedPartNumber(basePart.SparePartNo, faker),
                        SparePartSerialCode = GeneratePartSerialCode(
                            GetPartTypeFromName(basePart.Name),
                            GetManufacturerNameById(manufacturerIds, basePart.ManufacturerGuid, faker),
                            faker
                        ),
                        Name = basePart.Name,
                        Description = GenerateVariantDescription(basePart.Description, faker),
                        ManufacturerGuid = basePart.ManufacturerGuid,
                        CategoryGuid = basePart.CategoryGuid,
                        SupplierGuid = basePart.SupplierGuid,
                        LocationGuid = basePart.LocationGuid,
                        TypeNo = basePart.TypeNo,
                        Notes = faker.Random.Bool(0.6f) ? GenerateHistoricalPartNotes(basePart.Name, faker) : null
                    });
                }
            }

            // Hvis vi har genereret for mange, trim listen
            if (spareParts.Count > count)
            {
                spareParts = spareParts.Take(count).ToList();
            }
            // Hvis vi har genereret for få, generer nogle helt nye
            else if (spareParts.Count < count)
            {
                int remaining = count - spareParts.Count;
                var newParts = GenerateSpareParts(
                    remaining, null, unitGuid, manufacturerIds, categoryIds, supplierIds, locationIds, faker);
                spareParts.AddRange(newParts);
            }

            return spareParts;
        }

        // Generer nye reservedele fra bunden

        // Definer realistiske reservedelstyper
        var partTypes = new List<string>();

        // Tilføj alle parttyper fra vores forskellige kategorier
        foreach (var category in ComponentsByCategory.Values)
        {
            partTypes.AddRange(category);
        }

        // Tilføj dominerende industrispecifikke komponenter
        foreach (var industry in IndustrySpecificComponents.Values)
        {
            partTypes.AddRange(industry);
        }

        // Fjern dubletter
        partTypes = partTypes.Distinct().ToList();

        // Generer det ønskede antal reservedele
        for (int i = 0; i < count; i++)
        {
            string partType = faker.PickRandom(partTypes);

            // Tag højde for familier af produkter
            string partFamily = "";
            if (ProductFamilies.ContainsKey(GetBasePartType(partType)))
            {
                partFamily = $" {faker.PickRandom(ProductFamilies[GetBasePartType(partType)])}";
            }

            string name = GenerateRealisticPartName(partType, partFamily, faker);

            // Vælg tilfældige men passende relaterede data
            Guid manufacturerId = faker.PickRandom(manufacturerIds);
            Guid categoryId = GetAppropriateCategory(categoryIds, partType, faker);
            Guid supplierId = faker.PickRandom(supplierIds);
            Guid locationId = faker.PickRandom(locationIds);

            // Generer særlige numre og koder
            string partNumber = GeneratePartNumber(partType, faker);
            string serialCode = GeneratePartSerialCode(
                partType,
                GetManufacturerNameById(manufacturerIds, manufacturerId, faker),
                faker
            );
            string typeNumber = GenerateTypeNumber(partType, faker);

            // Opret reservedelen
            var sparePart = new SparePart
            {
                Id = Guid.NewGuid(),
                master_id = null,
                UnitGuid = unitGuid,
                SparePartNo = partNumber,
                SparePartSerialCode = serialCode,
                Name = name,
                Description = GeneratePartDescription(name, partType, faker),
                ManufacturerGuid = manufacturerId,
                CategoryGuid = categoryId,
                SupplierGuid = supplierId,
                LocationGuid = locationId,
                TypeNo = typeNumber,
                Notes = faker.Random.Bool(0.7f) ? GeneratePartNotes(name, partType, faker) : null
            };

            spareParts.Add(sparePart);
        }

        return spareParts;
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

    // Generer historiske reservedele
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
        // Vi genbruger logikken fra GenerateSpareParts med activeSpareParts som base
        return GenerateSpareParts(
            count, activeSpareParts, unitGuid, manufacturerIds, categoryIds, supplierIds, locationIds, faker);
    }

    // Generer komponent-reservedel relationer
    static List<ComponentPart> GenerateComponentParts(
        List<Component> components,
        List<SparePart> spareParts,
        Guid unitGuid,
        Faker faker)
    {
        var componentParts = new List<ComponentPart>();

        // Kun inkluder komponenter på niveau 3 eller højere (mere detaljerede niveauer)
        var detailedComponents = components
            .Where(c => c.Level >= 3)
            .ToList();

        foreach (var component in detailedComponents)
        {
            // Bestem hvor mange reservedele denne komponent skal bruge
            // Baseret på niveau og kompleksitet
            int partsCount = DeterminePartCount(component.Level, component.Name, faker);

            // Find passende reservedele baseret på komponentnavnet
            var matchingParts = FindMatchingParts(spareParts, component.Name, component.UnitGuid, partsCount, faker);

            foreach (var part in matchingParts)
            {
                // Bestem antal, position og noter baseret på komponent og reservedel
                int quantity = DeterminePartQuantity(part.Name, component.Name, faker);
                string position = GeneratePartPosition(part.Name, component.Name, faker);
                string notes = GenerateAssemblyNotes(part.Name, component.Name, faker);

                componentParts.Add(new ComponentPart
                {
                    Id = Guid.NewGuid(),
                    master_id = null,
                    UnitGuid = unitGuid,
                    ComponentGuid = component.Id,
                    SparePartGuid = part.Id,
                    Quantity = quantity,
                    Position = position,
                    Notes = faker.Random.Bool(0.3f) ? notes : null
                });
            }
        }

        return componentParts;
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

    // Database metoder

    static async Task BulkInsertAsync<T>(string tableName, List<T> items)
    {
        // Simuleret bulk-insert - i virkeligheden skal der laves en ordentlig databaseforbindelse
        Console.WriteLine($"Inserted {items.Count} items into {tableName}");

        // Her ville der normalt være kode til at udføre bulk insert i SQL Server
        // f.eks. ved hjælp af SqlBulkCopy

        await Task.Delay(100); // Simuler ventetid på databasen
    }
}

// Modelklasser der matcher databasen

public class Unit
{
    public Guid Id { get; set; }
    public string UnitNo { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public bool IsActive { get; set; }
}

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
}

public class Category
{
    public Guid Id { get; set; }
    public Guid? master_id { get; set; }
    public Guid UnitGuid { get; set; }
    public string CategoryNo { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
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
}
