using System.Collections.Generic;
using Bus_ticket.Models;

namespace Bus_ticket.Helpers;

public static class BusSeatLayoutGenerator
{
    public static List<SeatTemplate> Generate(int totalRows, int totalColumns, int totalFloors, string busType)
    {
        var layout = new List<SeatTemplate>();

        for (int floor = 1; floor <= totalFloors; floor++)
        {
            string floorPrefix = totalFloors > 1 ? (floor == 1 ? "A" : "B") : "";
            int seatCounter = 1;

            for (int row = 1; row <= totalRows; row++)
            {
                for (int col = 1; col <= totalColumns; col++)
                {
                    string seatNumber = totalFloors > 1
                        ? $"{floorPrefix}{seatCounter:D2}"
                        : $"{seatCounter:D2}";
                    string seatType = busType == "Luxury_Sleeper"
                        ? "Sleeper"
                        : (row <= 2 ? "VIP" : "Standard");

                    layout.Add(new SeatTemplate
                    {
                        SeatNumber = seatNumber,
                        Row = row,
                        Column = col,
                        Floor = floor,
                        SeatType = seatType
                    });

                    seatCounter++;
                }
            }
        }

        return layout;
    }
}
