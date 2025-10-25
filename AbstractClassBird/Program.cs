// Класс события - писк скворца

public abstract class FastAbstractObject
{
    public string uid = Guid.NewGuid().ToString();
    public double lastUpdated;
    public abstract (double, FastAbstractEvent) getNearestEvent();
    public abstract void Update(double timeSpan);
}

public abstract class FastAbstractEvent
{
    public string objId = null;
    public abstract void runEvent(FastAbstractWrapper wrapper, double timeSpan);
}

public abstract class FastAbstractWrapper
{
    protected Dictionary<string, FastAbstractObject> objects = new Dictionary<string, FastAbstractObject>();

    protected SortedList<double, FastAbstractEvent> eventList = new SortedList<double, FastAbstractEvent>();
    protected Dictionary<string, double> objectsEventTime = new Dictionary<string, double>();
    public double updatedTime;
    private HashSet<string> objectsKeyForUpdate;

    protected Action<string> writeDebug = Console.WriteLine;
    public Action<string> writeError = Console.WriteLine;

    const double precisionStep = 1e-15;

    public bool isDebug = false;



    public void WriteDebug(string debug)
    {
        if (isDebug)
            writeDebug(debug);
    }

    public double GetNearEventTime()
    {
        if (eventList.Count == 0)
            return double.MaxValue;
        return eventList.First().Key;
    }

    public void RemoveObjects(string uid)
    {
        objects.Remove(uid);
        objectsEventTime.Remove(uid);
        objectsKeyForUpdate.Remove(uid);
    }

    public List<string> getObjectKeys()
    {
        return objects.Keys.ToList();
    }

    public FastAbstractObject getObject(string key)
    {
        var obj = objects[key];
        obj.Update(updatedTime);
        objectsKeyForUpdate.Add(key);
        return objects[key];
    }


    public double NextDouble(double value)
    {
        long bits = BitConverter.DoubleToInt64Bits(value);

        // Увеличиваем на наименьшее возможное значение
        if (value >= 0)
            bits++;
        else
            bits--;

        return BitConverter.Int64BitsToDouble(bits);
    }

    protected double AddEvent(double timeSpan, FastAbstractEvent modelEvent, string objUid = null)
    {
        WriteDebug($"Event added {timeSpan} {modelEvent} {objUid}");

        if (timeSpan < updatedTime)
            throw new Exception("Time error!!");

        while (eventList.ContainsKey(timeSpan))
        {
            timeSpan = NextDouble(timeSpan);
        }
        modelEvent.objId = objUid;
        eventList.Add(timeSpan, modelEvent);
        if (!(objUid is null))
        {
            objectsEventTime.Add(objUid, timeSpan);
        }
        return timeSpan;
    }

    public bool Next()
    {
        if (eventList.Count == 0)
            return false;
        objectsKeyForUpdate = new HashSet<string>();
        var task = eventList.First();
        updatedTime = task.Key;
        eventList.Remove(task.Key);
        if (!(task.Value.objId is null))
        {
            getObject(task.Value.objId);
        }
        WriteDebug($"Run Event {task}");
        task.Value.runEvent(this, task.Key);
        foreach (var objKey in objectsKeyForUpdate)
        {
            if (objectsEventTime.ContainsKey(objKey))
            {
                eventList.Remove(objectsEventTime[objKey]);
                objectsEventTime.Remove(objKey);
            }
            var ev = objects[objKey].getNearestEvent();
            if (!(ev.Item2 is null))
            {
                AddEvent(ev.Item1, ev.Item2, objKey);
            }
        }
        return true;
    }

    public bool Next(double timeShift)
    {
        if (GetNearEventTime() > updatedTime + timeShift)
        {
            updatedTime = updatedTime + timeShift;
            foreach (var obj in objects.Values)
            {
                obj.Update(updatedTime);
            }
        }
        else
        {
            Next();
        }
        return true;
    }


    public void addObject(FastAbstractObject obj)
    {
        objects.Add(obj.uid, obj);
        var ev = obj.getNearestEvent();
        if (!(ev.Item2 is null))
        {
            AddEvent(ev.Item1, ev.Item2, obj.uid);
        }
    }

    public List<FastAbstractObject> GetFilteredObjects(Func<FastAbstractObject, bool> filterCriteria)
    {
        return objects.Values.Where(filterCriteria).ToList();
    }

    public List<FastAbstractObject> GetObjectsForUpdate()
    {
        if (objectsKeyForUpdate == null || objectsKeyForUpdate.Count == 0)
            return new List<FastAbstractObject>();

        return objects.Values
                     .Where(obj => obj != null && objectsKeyForUpdate.Contains(obj.uid))
                     .ToList();
    }

}



// Упрощенный класс для одномерного случая
public class Vector1D
{
    public double X { get; set; }

    public Vector1D(double x = 0)
    {
        X = x;
    }

    public static Vector1D operator +(Vector1D a, Vector1D b) => new Vector1D(a.X + b.X);
    public static Vector1D operator -(Vector1D a, Vector1D b) => new Vector1D(a.X - b.X);
    public static Vector1D operator *(Vector1D a, double scalar) => new Vector1D(a.X * scalar);

    public double Length => Math.Abs(X);

    public override string ToString() => $"{X:F2}";
}

// Класс скворца для одномерного случая
public class Starling : FastAbstractObject
{
    public string Name { get; set; }
    public Vector1D Position { get; set; }
    public Vector1D Velocity { get; set; }
    public Vector1D Acceleration { get; set; }
    public double Mass { get; set; } = 1.0;

    // Параметры для силы пружины
    public double SpringConstant { get; set; } = 1.0;
    public double TargetDistance { get; set; } = 2.0; // R_n^0

    // Лидер роя (A_0)
    public Starling Leader { get; set; }

    // Траектория лидера X_k^0 - теперь одномерная
    public Func<double, double> LeaderTrajectory { get; set; }

    public Starling(string name, double initialPosition)
    {
        Name = name;
        Position = new Vector1D(initialPosition);
        Velocity = new Vector1D();
        Acceleration = new Vector1D();
        lastUpdated = 0;
    }

    public override (double, FastAbstractEvent) getNearestEvent()
    {
        // Следующее событие через фиксированный интервал времени
        return (lastUpdated + 0.1, new StarlingUpdateEvent());
    }

    public override void Update(double timeSpan)
    {
        double deltaTime = timeSpan - lastUpdated;

        if (deltaTime > 0)
        {
            // Обновляем позицию и скорость
            Velocity = Velocity + Acceleration * deltaTime;
            Position = Position + Velocity * deltaTime;

            // Сбрасываем ускорение для следующего расчета
            Acceleration = new Vector1D();
        }

        lastUpdated = timeSpan;
    }

    public void ApplyForce(Vector1D force)
    {
        Acceleration = Acceleration + force * (1.0 / Mass);
    }

    // Вычисляем силу пружины F = -K(R_n^0 - R_n) для одномерного случая
    public void CalculateSpringForce()
    {
        if (Leader != null && LeaderTrajectory != null)
        {
            // Получаем позицию лидера в текущее время
            double leaderPosition = LeaderTrajectory(lastUpdated);

            // Расстояние до лидера
            double currentDistance = leaderPosition - Position.X;

            // Разность между целевым и текущим расстоянием
            double distanceDifference = TargetDistance - Math.Abs(currentDistance);

            // Определяем направление силы
            double forceDirection = Math.Sign(currentDistance);

            if (Math.Abs(distanceDifference) > 1e-10 && Math.Abs(currentDistance) > 1e-10)
            {
                // Сила пружины F = -K * (R_n^0 - |R_n|) * sign(R_n)
                double springForce = SpringConstant * distanceDifference * forceDirection;

                ApplyForce(new Vector1D(springForce));
            }
        }
    }
}

// Событие обновления состояния скворца
public class StarlingUpdateEvent : FastAbstractEvent
{
    public override void runEvent(FastAbstractWrapper wrapper, double timeSpan)
    {
        var swarmWrapper = wrapper as StarlingSwarmWrapper;
        if (swarmWrapper != null)
        {
            foreach (var starling in swarmWrapper.GetStarlings())
            {
                if (starling.Leader != null) // Не применяем силу к лидеру
                {
                    starling.CalculateSpringForce();
                }
            }
        }
    }
}

// Обертка для роя скворцов
public class StarlingSwarmWrapper : FastAbstractWrapper
{
    private Dictionary<string, Starling> starlings = new Dictionary<string, Starling>();

    public void AddStarling(Starling starling)
    {
        addObject(starling);
        starlings.Add(starling.uid, starling);
    }

    public List<Starling> GetStarlings()
    {
        return starlings.Values.ToList();
    }

    public Starling GetStarling(string uid)
    {
        return starlings.ContainsKey(uid) ? starlings[uid] : null;
    }

    public Starling GetLeader()
    {
        return starlings.Values.FirstOrDefault(s => s.Leader == null);
    }

    // Загрузка скворцов из файла (теперь только X координата)
    public void LoadStarlingsFromFile(string filename)
    {
        try
        {
            var lines = File.ReadAllLines(filename);
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length >= 2)
                {
                    string name = parts[0].Trim();
                    double x = double.Parse(parts[1].Trim());

                    var starling = new Starling(name, x);
                    AddStarling(starling);
                }
            }

            Console.WriteLine($"Загружено {starlings.Count} скворцов из файла {filename}");
        }
        catch (Exception ex)
        {
            writeError($"Ошибка загрузки файла: {ex.Message}");
        }
    }

    // Настройка лидера и его траектории (одномерная)
    public void SetupLeader(string leaderName, Func<double, double> trajectory)
    {
        var leader = starlings.Values.FirstOrDefault(s => s.Name == leaderName);
        if (leader != null)
        {
            // Лидер не имеет лидера самого себя
            leader.Leader = null;
            leader.LeaderTrajectory = trajectory;

            // Все остальные скворцы следуют за этим лидером
            foreach (var starling in starlings.Values.Where(s => s.Name != leaderName))
            {
                starling.Leader = leader;
                starling.LeaderTrajectory = trajectory;
            }

            Console.WriteLine($"Лидер установлен: {leaderName}");
        }
        else
        {
            writeError($"Скворец с именем {leaderName} не найден!");
        }
    }

    // Вывод текущего состояния роя
    public void PrintSwarmState()
    {
        Console.WriteLine($"Время: {updatedTime:F2}");
        var leader = GetLeader();

        if (leader != null)
        {
            double leaderPos = leader.LeaderTrajectory != null ?
                leader.LeaderTrajectory(updatedTime) : leader.Position.X;
            Console.WriteLine($"Лидер {leader.Name}: Позиция {leaderPos:F2}");
        }

        foreach (var starling in GetStarlings().OrderBy(s => s.Name))
        {
            if (starling.Leader != null)
            {
                double distanceToLeader = Math.Abs(starling.Leader.LeaderTrajectory(updatedTime) - starling.Position.X);
                Console.WriteLine($"{starling.Name}: Позиция {starling.Position}, " +
                                $"Скорость {starling.Velocity}, " +
                                $"Расстояние до лидера: {distanceToLeader:F2}");
            }
        }
        Console.WriteLine();
    }

    // Получить позиции всех скворцов для визуализации
    public Dictionary<string, double> GetPositions()
    {
        var positions = new Dictionary<string, double>();
        var leader = GetLeader();

        if (leader != null && leader.LeaderTrajectory != null)
        {
            positions.Add(leader.Name, leader.LeaderTrajectory(updatedTime));
        }

        foreach (var starling in GetStarlings().Where(s => s.Leader != null))
        {
            positions.Add(starling.Name, starling.Position.X);
        }

        return positions;
    }
}

// Пример использования
class Program
{
    static void Main(string[] args)
    {
        // Создаем файл со скворцами (только X координаты)
        CreateStarlingsFile("starlings.txt");

        // Создаем и настраиваем рой
        var swarm = new StarlingSwarmWrapper();
        swarm.isDebug = false;

        // Загружаем скворцов из файла
        swarm.LoadStarlingsFromFile("starlings.txt");

        // Задаем траекторию для лидера A_0: X_k^0 = 2*t - линейное движение
        Func<double, double> leaderTrajectory = (time) => 2 * time;

        // Альтернативные траектории для экспериментов:
        // Func<double, double> leaderTrajectory = (time) => 5 * Math.Sin(time * 0.5); // Колебания
        // Func<double, double> leaderTrajectory = (time) => time < 2 ? 0 : 3 * (time - 2); // Начинает движение после 2 сек

        // Назначаем лидера
        swarm.SetupLeader("A_0", leaderTrajectory);

        // Настраиваем параметры пружины для всех скворцов
        foreach (var starling in swarm.GetStarlings().Where(s => s.Leader != null))
        {
            starling.SpringConstant = 2.0;    // K - жесткость пружины
            starling.TargetDistance = 3.0;    // R_n^0 - целевое расстояние
        }

        Console.WriteLine("Начальное состояние роя:");
        swarm.PrintSwarmState();

        // Запускаем симуляцию
        int steps = 50;
        for (int i = 0; i < steps; i++)
        {
            swarm.Next(0.1); // Шаг симуляции 0.1 секунды

            if (i % 5 == 0) // Выводим состояние каждые 5 шагов
            {
                swarm.PrintSwarmState();
            }
        }

        Console.WriteLine("Симуляция завершена.");

        // Выводим финальные позиции
        Console.WriteLine("\nФинальные позиции:");
        var finalPositions = swarm.GetPositions();
        foreach (var pos in finalPositions.OrderBy(p => p.Value))
        {
            Console.WriteLine($"{pos.Key}: {pos.Value:F2}");
        }
    }

    static void CreateStarlingsFile(string filename)
    {
        // Формат: имя, позиция_X
        var starlings = new[]
        {
            "A_0, 0",      // Лидер
            "A_1, -2",
            "A_2, -4",
            "A_3, 2",
            "A_4, 4",
            "A_5, -3",
            "A_6, 3"
        };

        File.WriteAllLines(filename, starlings);
        Console.WriteLine($"Создан файл {filename} с {starlings.Length} скворцами");
    }
}

// Класс скворца

