using Foxnet.Session;

namespace Adventure.Expeditions;

public sealed partial class ExpeditionEngine
{
    public CellView ViewCell(WorldPosition position)
    {
        ObservedCell? current = _visible.GetValueOrDefault(position);
        ObservedCell? previous = State.Explored.GetValueOrDefault(position.LevelId)?.GetValueOrDefault(_map.CellIndex(position));
        bool rememberedInterior = current is { Detail: false } && previous is { Detail: true } && State.Mode != ControlMode.Laser;
        bool live = current != null && !rememberedInterior;
        var sample = live ? current : previous;
        var visibility = live ? CellVisibility.Visible : sample != null ? CellVisibility.Remembered : CellVisibility.Unknown;
        var drone = ActiveDrone.Position == position ? ActiveDrone : null;
        if (drone == null && current is { Detail: true })
            drone = State.Drones.Values.FirstOrDefault(candidate => candidate.Discovered && candidate.Position == position);

        bool crate = live && current is { Detail: true } && State.DropCrates.Exists(box => box.Position == position);
        return new CellView(visibility, drone != null ? 'X' : crate ? '■' : sample?.Glyph ?? ' ',
            drone != null ? _definitions[drone.Id].Color : crate ? ConsoleColor.DarkYellow : live ? sample!.Color : ConsoleColor.DarkGray,
            drone?.Id, State.Laser == position);
    }

    private void RefreshVisibility(bool remember)
    {
        _visible.Clear();
        if (State.AwaitingFirstSurvey)
        {
            _visible[ActiveDrone.Position] = _map.Sample(ActiveDrone.Position);
            if (remember)
                RememberSurvey();
            return;
        }

        Survey(State.Laser, orbital: true);
        var drone = ActiveDrone;
        Dictionary<WorldPosition, ObservedCell>? orbital = null;
        if (State.Mode != ControlMode.Laser && drone.LidarScanned && drone.Slots.ContainsKey(EquipmentSlot.Lidar))
        {
            if (drone.Powered)
                Survey(drone.Position, orbital: false);
            else if (remember)
            {
                orbital = new(_visible);
                Survey(drone.Position, orbital: false);
            }
        }

        if (!remember)
            return;
        RememberSurvey();
        foreach (var candidate in State.Drones.Values)
        {
            if (_visible.GetValueOrDefault(candidate.Position) is { Detail: true })
                candidate.Discovered = true;
        }

        if (orbital != null)
        {
            _visible.Clear();
            foreach (var (position, sample) in orbital)
                _visible.Add(position, sample);
        }
    }

    private void SurveyStartingBay()
    {
        var station = Definition.Station;
        var bay = station.Bays.Single(candidate => candidate.DroneId == Definition.StartDroneId);
        Survey(station.At(bay.ParkingX, bay.ParkingY), orbital: true);
        foreach (int x in new[] { bay.GateX, bay.ParkingX, bay.TerminalX })
        {
            var position = station.At(x, bay.ParkingY);
            _visible[position] = _map.Sample(position);
        }
        RememberSurvey();
    }

    private void RememberSurvey()
    {
        foreach (var (position, sample) in _visible)
        {
            if (!State.Explored.TryGetValue(position.LevelId, out var level))
            {
                level = [];
                State.Explored.Add(position.LevelId, level);
            }
            int index = _map.CellIndex(position);
            if (sample.Detail || level.GetValueOrDefault(index) is not { Detail: true })
                level[index] = sample;
        }
    }

    private void Survey(WorldPosition origin, bool orbital)
    {
        if (orbital && Levels[origin.LevelId].Underground)
            return;
        string? building = _map.Interior(origin);
        if (!orbital && building == null)
            return;

        int radius = Definition.Balance.ScanRadius;
        for (int y = -radius; y <= radius; y++)
        for (int x = -radius; x <= radius; x++)
        {
            if (x * x + y * y > radius * radius || !_map.TryOffset(origin, x, y, out var position))
                continue;

            string? interior = _map.Interior(position);
            if (!orbital && interior != building)
                continue;
            if (!CanSee(origin, x, y, orbital, building))
                continue;

            bool roof = orbital && interior != null;
            _visible[position] = roof ? new ObservedCell('▒', ConsoleColor.DarkGray, false)
                : _donors.ContainsKey(position) ? new ObservedCell('d', ConsoleColor.DarkYellow, true) : _map.Sample(position);
        }
    }

    private bool CanSee(WorldPosition origin, int x, int y, bool orbital, string? originBuilding)
    {
        int length = Math.Max(Math.Abs(x), Math.Abs(y));
        int previousX = 0;
        int previousY = 0;
        for (int step = 1; step <= length; step++)
        {
            int dx = (int)Math.Round((double)x * step / length, MidpointRounding.AwayFromZero);
            int dy = (int)Math.Round((double)y * step / length, MidpointRounding.AwayFromZero);
            if (dx != previousX && dy != previousY && Blocks(dx, previousY) && Blocks(previousX, dy))
                return false;
            if (step < length && Blocks(dx, dy))
                return false;
            previousX = dx;
            previousY = dy;
        }
        return true;

        bool Blocks(int dx, int dy)
        {
            if (!_map.TryOffset(origin, dx, dy, out var position))
                return true;
            if (orbital && _map.Interior(position) is string building)
                return building != originBuilding || _map.BlocksScan(position, roofProjection: true);
            return _map.BlocksScan(position);
        }
    }
}
