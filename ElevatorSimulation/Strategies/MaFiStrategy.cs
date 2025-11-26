namespace ElevatorSimulation.Strategies;

internal class MaFiStrategy : IElevatorStrategy
{
    // M Prefix = multiply
    // A Prefix = add
    // AM Prefix = add/multiply (setting to 0 means no effect)

    public double MPickUpBias = 1.5;
    public double MDropOffBias = 1.9;
    public double MOpenDoorBias = 6.7;
    public double AMHeatMapBias = 1.30;
    public double MPrioritizeCurrentDirectionBias = 1.0;
    public double MStarvationMultiplier = 1.1;
    public int StarvationThreshold = 25;
    public double TravelCostPerFloor = 0.15;
    public int HeatMapRadius = 1;

    public bool EnableStarvationPrevention = true;
    public bool EnableTravelCost = true;

    public MoveResult DecideNextMove(ElevatorSystem elevator)
    {
        // The resulting score is the cummulative time of all passengers waiting + travelling
        // so we want to minimize that

        // If no riders and no requests, go towards the middle
        if (elevator.PendingRequests.Count == 0 && elevator.ActiveRiders.Count == 0)
        {
            var middle = elevator.Building.MinFloor + (elevator.Building.MaxFloor - elevator.Building.MinFloor) / 2;

            return MoveTowardsFloor(elevator, middle);
        }

        var floorMoveScores = GetScores(elevator);

        // Find the floor with the highest score, and move towards it
        // if it's the current floor, open doors
        var bestFloor = floorMoveScores.MaxBy(kv => kv.Value).Key;
        if (bestFloor == elevator.CurrentElevatorFloor)
        {
            return MoveResult.OpenDoors;
        }
        else
        {
            return MoveTowardsFloor(elevator, bestFloor);
        }
    }

    private Dictionary<int, double> GetScores(ElevatorSystem elevator)
    {
        var floorMoveScores = new Dictionary<int, double>();

        // Pre-compute floor groupings once (performance optimization)
        var pickupsByFloor = GroupRequestsByFloor(elevator.PendingRequests);
        var dropoffsByFloor = GroupRidersByDestination(elevator.ActiveRiders);
        var directionInfo = AnalyzeDirections(elevator);

        SetBaseRiderScores(elevator, floorMoveScores, pickupsByFloor, dropoffsByFloor);
        
        if (AMHeatMapBias > 0.01)
        {
            AddHeatMapScores(elevator, floorMoveScores);
        }
        
        AddSameDirectionScore(elevator, floorMoveScores, directionInfo);
        
        if (EnableStarvationPrevention)
        {
            AddStarvationPreventionScore(elevator, floorMoveScores, pickupsByFloor);
        }
        
        if (EnableTravelCost)
        {
            ApplyTravelCost(elevator, floorMoveScores);
        }

        AddOpenDoorScore(elevator, floorMoveScores, pickupsByFloor, dropoffsByFloor); // Always at the end...

        return floorMoveScores;
    }

    // Performance optimization: Pre-compute floor lookups instead of repeated LINQ queries
    private Dictionary<int, List<RiderRequest>> GroupRequestsByFloor(List<RiderRequest> requests)
    {
        var groups = new Dictionary<int, List<RiderRequest>>();
        foreach (var request in requests)
        {
            if (!groups.ContainsKey(request.From))
                groups[request.From] = new List<RiderRequest>();
            groups[request.From].Add(request);
        }
        return groups;
    }

    private Dictionary<int, List<RiderRequest>> GroupRidersByDestination(List<RiderRequest> riders)
    {
        var groups = new Dictionary<int, List<RiderRequest>>();
        foreach (var rider in riders)
        {
            if (!groups.ContainsKey(rider.To))
                groups[rider.To] = new List<RiderRequest>();
            groups[rider.To].Add(rider);
        }
        return groups;
    }

    // Cache direction availability analysis (single pass)
    private record DirectionInfo(bool HasUpRequests, bool HasDownRequests);
    
    private DirectionInfo AnalyzeDirections(ElevatorSystem elevator)
    {
        bool hasUp = false;
        bool hasDown = false;
        int currentFloor = elevator.CurrentElevatorFloor;

        foreach (var request in elevator.PendingRequests)
        {
            if (request.From > currentFloor) hasUp = true;
            if (request.From < currentFloor) hasDown = true;
            if (hasUp && hasDown) break; // Early exit
        }

        if (!hasUp || !hasDown) // Only check riders if needed
        {
            foreach (var rider in elevator.ActiveRiders)
            {
                if (rider.To > currentFloor) hasUp = true;
                if (rider.To < currentFloor) hasDown = true;
                if (hasUp && hasDown) break;
            }
        }

        return new DirectionInfo(hasUp, hasDown);
    }

    private void SetBaseRiderScores(ElevatorSystem elevator, Dictionary<int, double> floorMoveScores,
        Dictionary<int, List<RiderRequest>> pickupsByFloor, Dictionary<int, List<RiderRequest>> dropoffsByFloor)
    {
        for (int floor = elevator.Building.MinFloor; floor <= elevator.Building.MaxFloor; floor++)
        {
            double score = 0d;

            // Use pre-computed lookups instead of LINQ
            int waitingCount = pickupsByFloor.TryGetValue(floor, out var pickups) ? pickups.Count : 0;
            int dropoffCount = dropoffsByFloor.TryGetValue(floor, out var dropoffs) ? dropoffs.Count : 0;

            // Add score for pickup and dropoff
            // Prioritize spaces with most riders waiting + most riders to drop off
            score += waitingCount * MPickUpBias;
            score += dropoffCount * MDropOffBias;

            floorMoveScores[floor] = score;
        }
    }

    // Improved heatmap with configurable radius and weighted averaging
    private void AddHeatMapScores(ElevatorSystem elevator, Dictionary<int, double> floorMoveScores)
    {
        var heatmapScores = new Dictionary<int, double>(floorMoveScores.Count);

        for (int floor = elevator.Building.MinFloor; floor <= elevator.Building.MaxFloor; floor++)
        {
            double heatmapScore = 0d;
            double totalWeight = 0d;

            // Consider neighbors within radius
            for (int offset = -HeatMapRadius; offset <= HeatMapRadius; offset++)
            {
                int neighborFloor = floor + offset;
                if (neighborFloor >= elevator.Building.MinFloor && 
                    neighborFloor <= elevator.Building.MaxFloor)
                {
                    // Closer floors have more weight
                    double weight = 1.0 / (Math.Abs(offset) + 1.0);
                    heatmapScore += floorMoveScores[neighborFloor] * weight;
                    totalWeight += weight;
                }
            }

            heatmapScore /= totalWeight; // Weighted average
            heatmapScore *= AMHeatMapBias;
            
            heatmapScores[floor] = heatmapScore;
        }

        // Apply heatmap scores
        for (int floor = elevator.Building.MinFloor; floor <= elevator.Building.MaxFloor; floor++)
        {
            floorMoveScores[floor] += heatmapScores[floor];
        }
    }

    private void AddSameDirectionScore(ElevatorSystem elevator, Dictionary<int, double> floorMoveScores,
        DirectionInfo directionInfo)
    {
        // If the elevator is moving up, prioritize floors above
        // If the elevator is moving down, prioritize floors below

        if (elevator.CurrentElevatorDirection == Direction.Up && directionInfo.HasUpRequests)
        {
            for (int floor = elevator.CurrentElevatorFloor + 1; floor <= elevator.Building.MaxFloor; floor++)
            {
                floorMoveScores[floor] *= MPrioritizeCurrentDirectionBias;
            }
        }
        else if (elevator.CurrentElevatorDirection == Direction.Down && directionInfo.HasDownRequests)
        {
            for (int floor = elevator.Building.MinFloor; floor < elevator.CurrentElevatorFloor; floor++)
            {
                floorMoveScores[floor] *= MPrioritizeCurrentDirectionBias;
            }
        }
    }

    // New: Starvation prevention - exponentially increase priority for long-waiting requests
    private void AddStarvationPreventionScore(ElevatorSystem elevator, Dictionary<int, double> floorMoveScores,
        Dictionary<int, List<RiderRequest>> pickupsByFloor)
    {
        foreach (var (floor, requests) in pickupsByFloor)
        {
            foreach (var request in requests)
            {
                int waitTime = elevator.CurrentTime - request.CreatedAt;
                if (waitTime > StarvationThreshold)
                {
                    // Exponentially increase priority based on wait time
                    double starvationBonus = Math.Pow(MStarvationMultiplier, (waitTime - StarvationThreshold) / 5.0);
                    floorMoveScores[floor] *= starvationBonus;
                }
            }
        }
    }

    // New: Apply travel cost - penalize distant floors to encourage efficiency
    private void ApplyTravelCost(ElevatorSystem elevator, Dictionary<int, double> floorMoveScores)
    {
        int currentFloor = elevator.CurrentElevatorFloor;
        
        for (int floor = elevator.Building.MinFloor; floor <= elevator.Building.MaxFloor; floor++)
        {
            int distance = Math.Abs(floor - currentFloor);
            double costPenalty = 1.0 - (distance * TravelCostPerFloor);
            
            // Ensure penalty doesn't make scores negative
            costPenalty = Math.Max(costPenalty, 0.1);
            
            floorMoveScores[floor] *= costPenalty;
        }
    }

    private void AddOpenDoorScore(ElevatorSystem elevator, Dictionary<int, double> floorMoveScores,
        Dictionary<int, List<RiderRequest>> pickupsByFloor, Dictionary<int, List<RiderRequest>> dropoffsByFloor)
    {
        var currentFloor = elevator.CurrentElevatorFloor;
        
        // Use pre-computed lookups
        bool hasPickup = pickupsByFloor.ContainsKey(currentFloor);
        bool hasDropoff = dropoffsByFloor.ContainsKey(currentFloor);
        
        if (hasPickup || hasDropoff)
        {
            floorMoveScores[currentFloor] *= MOpenDoorBias;
        }
    }

    private MoveResult MoveTowardsFloor(ElevatorSystem elevator, int targetFloor)
    {
        if (elevator.CurrentElevatorFloor < targetFloor)
            return MoveResult.MoveUp;
        else if (elevator.CurrentElevatorFloor > targetFloor)
            return MoveResult.MoveDown;
        else
            return MoveResult.OpenDoors;
    }
}
