class Node
{
    public string NodeID { get; set; }

    //Last Position
    public double LastLatitude { get; set; }
    public double LastLongitude { get; set; }

    public DateTime LastUpdate { get; set; }

    public double GetDistanceTo(double latitude, double longitude)
    {
        var dLat = (latitude - LastLatitude) * (Math.PI / 180);
        var dLon = (longitude - LastLongitude) * (Math.PI / 180);
        var a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(LastLatitude * (Math.PI / 180))
                * Math.Cos(latitude * (Math.PI / 180))
                * Math.Sin(dLon / 2)
                * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        const double radiusOfEarthInKm = 6371.0;
        return radiusOfEarthInKm * c;
    }

    public Node(string id)
    {
        NodeID = id;
        LastLatitude = 0.0;
        LastLongitude = 0.0;
        LastUpdate = DateTime.Now;
    }
}
