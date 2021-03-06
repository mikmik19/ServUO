using System;
using Server;
using System.Collections.Generic;
using Server.Mobiles;
using System.Linq;
using Server.Network;
using Server.Commands;

namespace Server.Items
{
    public class MyrmidexHill : Item
    {
        private Type[] _SpawnList =
        {
            typeof(MyrmidexLarvae), typeof(MyrmidexDrone), typeof(MyrmidexWarrior)
        };

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime NextSpawn { get; set; }

        public EodonTribeRegion Zone { get; set; }

        public List<BaseCreature> Spawn { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public Mobile Focus { get; set; }

        public int SpawnCount { get { return Utility.RandomMinMax(8, 12); } }

        public MyrmidexHill(EodonTribeRegion zone, Mobile focus)
            : base(8754)
        {
            Movable = false;

            Focus = focus;
            Zone = zone;
            Spawn = new List<BaseCreature>();
        }

        public override bool HandlesOnMovement { get { return NextSpawn < DateTime.UtcNow; } }
        public override void OnMovement(Mobile m, Point3D oldLocation)
        {
            if (m.InRange(this.Location, 7) && (m is PlayerMobile || (m is BaseCreature && ((BaseCreature)m).GetMaster() is PlayerMobile)))
            {
                Focus = m;
                DoSpawn();
            }
        }

        public void DoSpawn()
        {
            Map map = this.Map;

            if (Spawn == null)
                return;

            ColUtility.ForEach(Spawn.Where(bc => bc == null || !bc.Alive || bc.Deleted), bc => Spawn.Remove(bc));

            if (map != null && map != Map.Internal && !this.Deleted)
            {
                NextSpawn = DateTime.UtcNow + TimeSpan.FromMinutes(Utility.RandomMinMax(2, 5));

                int time = 333;
                for (int i = 0; i < SpawnCount - Spawn.Count; i++)
                {
                    Timer.DelayCall(TimeSpan.FromMilliseconds(time), () =>
                    {
                        Point3D p = this.Location;

                        for (int j = 0; j < 25; j++)
                        {
                            int x = Utility.RandomMinMax(this.X - 3, this.X + 3);
                            int y = Utility.RandomMinMax(this.Y - 3, this.Y + 3);
                            int z = map.GetAverageZ(x, y);

                            if (map.CanSpawnMobile(x, y, z) && this.InLOS(new Point3D(x, y, z)))
                            {
                                p = new Point3D(x, y, z);
                                break;
                            }
                        }

                        BaseCreature bc = Activator.CreateInstance(_SpawnList[Utility.Random(_SpawnList.Length)]) as BaseCreature;

                        if (bc != null)
                        {
                            Spawn.Add(bc);
                            bc.MoveToWorld(p, map);

                            Timer.DelayCall<BaseCreature>(creature => creature.Combatant = Focus, bc);
                        }
                    });

                    time += 333;
                }
            }
        }

        public void CheckSpawn()
        {
            if (Spawn == null)
                Delete();
            else
            {
                int count = 0;
                ColUtility.ForEach(Spawn.Where(bc => bc != null && bc.Alive), bc => count++);

                if (count == 0)
                    Delete();
            }
            
        }

        public override void Delete()
        {
            base.Delete();

            if (Spawn != null)
            {
                ColUtility.Free(Spawn);
                Spawn = null;
            }
        }

        public MyrmidexHill(Serial serial)
            : base(serial)
        {
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);

            writer.Write(Spawn == null ? 0 : Spawn.Count);

            if (Spawn != null)
            {
                Spawn.ForEach(bc => writer.Write(bc));
            }

            Timer.DelayCall(TimeSpan.FromSeconds(30), CheckSpawn);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            int count = reader.ReadInt();

            if (count > 0)
            {
                Spawn = new List<BaseCreature>();

                for (int i = 0; i < count; i++)
                {
                    BaseCreature bc = reader.ReadMobile() as BaseCreature;

                    if (bc != null)
                        Spawn.Add(bc);
                }
            }

            if (Spawn == null || Spawn.Count == 0)
                Delete();
            else
            {
                Timer.DelayCall(TimeSpan.FromSeconds(10), () =>
                    {
                        EodonTribeRegion r = Region.Find(this.Location, this.Map) as EodonTribeRegion;

                        if (r != null)
                            Zone = r;
                    });
            }
        }
    }

    public class EodonTribeRegion : Region
    {
        public static void Initialize()
        {
            _Zones[0] = new EodonTribeRegion(EodonTribe.Jukari, new Rectangle2D[] { new Rectangle2D(640, 2046, 115, 115) }, 6);
            _Zones[1] = new EodonTribeRegion(EodonTribe.Kurak, new Rectangle2D[] { new Rectangle2D(291, 1817, 125, 90) }, 6);
            _Zones[2] = new EodonTribeRegion(EodonTribe.Barrab, new Rectangle2D[] { new Rectangle2D(134, 1767, 33, 20), new Rectangle2D(142, 1786, 57, 80), new Rectangle2D(145, 1750, 20, 20) }, 5);
            _Zones[3] = new EodonTribeRegion(EodonTribe.Barako, new Rectangle2D[] { new Rectangle2D(620, 1677, 95, 100) }, 5);
            _Zones[4] = new EodonTribeRegion(EodonTribe.Urali, new Rectangle2D[] { new Rectangle2D(320, 1551, 160, 72) }, 5);
            _Zones[5] = new EodonTribeRegion(EodonTribe.Sakkhra, new Rectangle2D[] { new Rectangle2D(482, 1375, 200, 200) }, 8);
        }

        public static EodonTribeRegion[] _Zones = new EodonTribeRegion[6];

        public int MaxSpawns { get; private set; }
        public EodonTribe Tribe { get; set; }
        public int Spawns { get { return this.GetItemCount(i => i is MyrmidexHill); } }

        public EodonTribeRegion(EodonTribe tribe, Rectangle2D[] rec, int maxSpawns)
            : base(tribe.ToString() + " tribe", Map.TerMur, Region.DefaultPriority, rec)
        {
            Tribe = tribe;
            Register();

            MaxSpawns = maxSpawns;
        }

        public override void OnLocationChanged(Mobile m, Point3D oldLocation)
        {
            if (Tribe != EodonTribe.Barrab && Spawns < MaxSpawns)
            {
                double chance = Utility.RandomDouble();

                if (0.005 > chance && (m is PlayerMobile || (m is BaseCreature && ((BaseCreature)m).GetMaster() is PlayerMobile)) && m.AccessLevel == AccessLevel.Player)
                {
                    MyrmidexHill hill = new MyrmidexHill(this, m);
                    Point3D p = m.Location;

                    for (int i = 0; i < 10; i++)
                    {
                        int x = Utility.RandomMinMax(p.X - 5, p.X + 5);
                        int y = Utility.RandomMinMax(p.Y - 5, p.Y + 5);
                        int z = this.Map.GetAverageZ(x, y);

                        if (this.Map.CanFit(x, y, z, 16, false, false, true))
                        {
                            p = new Point3D(x, y, z);
                            break;
                        }
                    }

                    hill.MoveToWorld(p, this.Map);
                    hill.DoSpawn();
                }
            }
        }
    }
}