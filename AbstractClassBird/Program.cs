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
public class ChirpEvent : FastAbstractEvent
{
    public string chirperId;
    public double chirpTime;

    public ChirpEvent(string chirperId, double chirpTime)
    {
        this.chirperId = chirperId;
        this.chirpTime = chirpTime;
    }

    public override void runEvent(FastAbstractWrapper wrapper, double timeSpan)
    {
        var swarmWrapper = wrapper as SwarmWrapper;
        if (swarmWrapper != null)
        {
            swarmWrapper.ProcessChirp(chirperId, chirpTime, timeSpan);
        }
    }

    public override string ToString()
    {
        return $"ChirpEvent from {chirperId} at {chirpTime}";
    }
}

// Класс скворца
public class Starling : FastAbstractObject
{
    public double position; // Положение на прямой
    public double velocity; // Скорость
    public double lastChirpTime; // Время последнего писка
    public double nextChirpTime; // Время следующего писка
    public Dictionary<string, (double time, double position)> receivedChirps; // Полученные писки

    // Параметры поведения
    public double chirpInterval; // Интервал между писками
    public double safeDistance; // Зона безопасности
    public double communicationRange; // Дальность связи
    public double maxSpeed; // Максимальная скорость

    public Starling(double initialPosition, double initialVelocity = 0)
    {
        position = initialPosition;
        velocity = initialVelocity;
        lastChirpTime = 0;
        nextChirpTime = 1.0; // Первый писк через 1 единицу времени
        receivedChirps = new Dictionary<string, (double, double)>();

        // Параметры по умолчанию
        chirpInterval = 2.0;
        safeDistance = 2.0;
        communicationRange = 10.0;
        maxSpeed = 1.0;
    }

    public override (double, FastAbstractEvent) getNearestEvent()
    {
        return (nextChirpTime, new ChirpEvent(uid, nextChirpTime));
    }

    public override void Update(double currentTime)
    {
        lastUpdated = currentTime;

        // Обновление позиции
        double deltaTime = currentTime - lastUpdated;
        position += velocity * deltaTime;
    }

    // Обработка полученного писка
    public void ReceiveChirp(string fromId, double chirpTime, double receiveTime, double senderPosition)
    {
        // Сохраняем информацию о писке
        receivedChirps[fromId] = (receiveTime, senderPosition);

        // Принимаем решение о маневре
        DecideManeuver();
    }

    // Принятие решения о маневре
    private void DecideManeuver()
    {
        double totalForce = 0;
        int neighborCount = 0;

        foreach (var chirp in receivedChirps.Values)
        {
            double otherPosition = chirp.position;
            double distance = Math.Abs(position - otherPosition);

            if (distance < communicationRange && distance > 0)
            {
                // Сила отталкивания для избежания столкновений
                if (distance < safeDistance)
                {
                    double repulsionForce = 1.0 / (distance * distance) - 1.0 / (safeDistance * safeDistance);
                    if (otherPosition > position)
                        repulsionForce = -repulsionForce; // Двигаемся влево
                    else
                        repulsionForce = repulsionForce; // Двигаемся вправо

                    totalForce += repulsionForce;
                    neighborCount++;
                }
            }
        }

        if (neighborCount > 0)
        {
            // Обновляем скорость на основе сил
            double acceleration = totalForce / neighborCount;
            velocity += acceleration * 0.1; // Малый шаг для плавности

            // Ограничиваем скорость
            velocity = Math.Max(-maxSpeed, Math.Min(maxSpeed, velocity));
        }
        else
        {
            // Если соседей нет, замедляемся
            velocity *= 0.95;
        }
    }

    // Подготовка к следующему писку
    public void PrepareNextChirp(double currentTime)
    {
        lastChirpTime = currentTime;
        nextChirpTime = currentTime + chirpInterval;
    }
}

// Обертка для управления роем
public class SwarmWrapper : FastAbstractWrapper
{
    private Dictionary<string, Starling> starlings => objects.Values.OfType<Starling>().ToDictionary(s => s.uid);
    private double speedOfSound = 1.0; // Скорость распространения писка

    public void ProcessChirp(string chirperId, double chirpTime, double receiveTime)
    {
        var chirper = getObject(chirperId) as Starling;
        if (chirper == null) return;

        // Обновляем позицию чирпера на момент писка
        chirper.Update(chirpTime);
        double chirperPosition = chirper.position;

        // Отправляем писк всем другим скворцам
        foreach (var starling in starlings.Values)
        {
            if (starling.uid != chirperId)
            {
                // Рассчитываем время получения с учетом расстояния
                double distance = Math.Abs(starling.position - chirperPosition);
                double timeToReceive = chirpTime + distance / speedOfSound;

                if (timeToReceive <= receiveTime) // Писк уже должен был быть получен
                {
                    starling.Update(timeToReceive);
                    starling.ReceiveChirp(chirperId, chirpTime, timeToReceive, chirperPosition);
                }
            }
        }

        // Подготавливаем следующий писк
        chirper.PrepareNextChirp(receiveTime);
    }

    // Добавление скворца в рой
    public void AddStarling(Starling starling)
    {
        addObject(starling);
    }

    // Получение текущего состояния роя
    public List<(string id, double position, double velocity)> GetSwarmState()
    {
        return starlings.Values.Select(s => (s.uid, s.position, s.velocity)).ToList();
    }

    // Проверка безопасности дистанций
    public List<(string id1, string id2, double distance)> GetViolatedDistances()
    {
        var violations = new List<(string, string, double)>();
        var starlingList = starlings.Values.ToList();

        for (int i = 0; i < starlingList.Count; i++)
        {
            for (int j = i + 1; j < starlingList.Count; j++)
            {
                double distance = Math.Abs(starlingList[i].position - starlingList[j].position);
                if (distance < starlingList[i].safeDistance)
                {
                    violations.Add((starlingList[i].uid, starlingList[j].uid, distance));
                }
            }
        }

        return violations;
    }
}

// Пример использования
public class SwarmSimulation
{
    public static void Main(String[] args)
    {
        var swarm = new SwarmWrapper();
        var random = new Random();

        // Создаем начальный рой из 5 скворцов
        for (int i = 0; i < 5; i++)
        {
            var starling = new Starling(
                initialPosition: i * 3.0, // Начальные позиции с интервалом 3
                initialVelocity: (random.NextDouble() - 0.5) * 0.1 // Случайные небольшие скорости
            );
            swarm.AddStarling(starling);
        }

        // Запускаем симуляцию на 100 шагов
        for (int step = 0; step < 100; step++)
        {
            swarm.Next(0.1); // Шаг симуляции 0.1 единицы времени

            // Выводим состояние каждые 10 шагов
            if (step % 10 == 0)
            {
                Console.WriteLine($"Step {step}:");
                var state = swarm.GetSwarmState();
                foreach (var (id, position, velocity) in state)
                {
                    Console.WriteLine($"  Starling {id.Substring(0, 8)}: pos={position:F2}, vel={velocity:F2}");
                }

                var violations = swarm.GetViolatedDistances();
                if (violations.Count > 0)
                {
                    Console.WriteLine("  Safety violations:");
                    foreach (var (id1, id2, distance) in violations)
                    {
                        Console.WriteLine($"    {id1.Substring(0, 8)} - {id2.Substring(0, 8)}: {distance:F2}");
                    }
                }
                Console.WriteLine();
            }
        }
    }
}
