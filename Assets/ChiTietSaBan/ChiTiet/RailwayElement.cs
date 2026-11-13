
using SQLite;

[Table("RailwayElements")]
public class RailwayElement
{
    [PrimaryKey]
    public string ID { get; set; }

    public string ElementType { get; set; }
    public string ElementName { get; set; }
    public string Material { get; set; }

    public double LengthMm { get; set; }
    public double WidthMm { get; set; }
    public double HeightMm { get; set; }

    public double VolumeM3 { get; set; }
}
