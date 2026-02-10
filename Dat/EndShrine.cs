namespace Pathhack.Dat;


public static class EndShrineLevels
{
  private static readonly WeaponDef BadPlayerDoThePuzzle = new()
  {
    Name = "Divine punishment",
    BaseDamage = d(6, 6),
    DamageType = DamageTypes.Magic,
    Profiency = "_exotic",
    Price = 10000,
  };

  private static readonly MonsterDef ShrineAttendant = new()
  {
    Name = "Shrine Attendant",
    Family = "construct",
    CreatureType = CreatureTypes.Construct,
    AttackBonus = 1,
    DamageBonus = 0,
    Glyph = new('7', ConsoleColor.White),
    AC = 2,
    HpPerLevel = 8,
    // AC = 1,
    // HP = 1,
    Unarmed = NaturalWeapons.Claw_1d6,
    SpawnWeight = 0,
    MoralAxis = MoralAxis.Neutral,
    EthicalAxis = EthicalAxis.Neutral,
    Components = [
      new GrantAction(AttackWithWeapon.Instance),
      EquipSet.OneOf(MundaneArmory.Longsword, MundaneArmory.Spear, MundaneArmory.Mace),
    ],
  };

  private static readonly MonsterDef WrongShrineGuardian = new()
  {
    Name = "Shrine Guardian",
    Family = "construct",
    CreatureType = CreatureTypes.Construct,
    AttackBonus = 40,
    DamageBonus = 10,
    Glyph = new('7', ConsoleColor.Magenta),
    AC = 100,
    HpPerLevel = 100,
    // AC = 1,
    // HP = 1,
    Unarmed = NaturalWeapons.Claw_1d6,
    SpawnWeight = 0,
    MoralAxis = MoralAxis.Neutral,
    EthicalAxis = EthicalAxis.Neutral,
    Components = [
      new GrantAction(AttackWithWeapon.Instance),
      new Equip(BadPlayerDoThePuzzle)
    ],
  };

  public static readonly SpecialLevel EndShrine1 = new("endshrine_1", """
 2222222 3333333 4444444  .............................. 
 2.....2 3.....3 4.....4  ..............................
 2.._..2 3.._..3 4.._..4  ..............................
 2.....2 3.....3 4.....4  ..............................
 222+222 333+333 444+444  ..............................
    #       #       #     ..............................  
 111+1111111+1111111+111  ..............................
 1.....................1  ..............................
 1.....................1  ..............................
 1.....................1  |.............................
 1.....................+##|M........................<...
 1.....................1  |.............................
 1.....................1  ..............................
 1.....................1  ..............................
 111+1111111+1111111+111  ..............................
    #       #       #     ..............................     
 555+555 666+666 777+777  ..............................
 5.....5 6.....6 7.....7  ..............................
 5.._..5 6.._..6 7.._..7  ..............................
 5.....5 6.....6 7.....7  ..............................
 5555555 6666666 7777777  ..............................
""", PostRender: b =>
  {
    b.Level.NoInitialSpawns = true;
    b.Level.FirstIntro = "Before you stands the Cathedral of Sancta [fg=yellow]Iomedaea[/fg], once the heart of Lastwall's faith. When the Whispering Tyrant broke the nation, the priests sealed its doors rather than let it fall to undeath. The wards have held for a century.";
    b.Level.ReturnIntro = "The ruined spires of the ancient cathedral rise from the darkness ahead.";


    b.Stair(b['<'], TileType.StairsUp);

    b.Level.GetOrCreateState(b['M']).Message = "A faded sign reads: 'Closed by order of the Prelate. Do not enter.'";
    b.Level.GetOrCreateState(b['M']).Feature = new TileFeature("_lock");

    var shrines = b.Marks('_');
    int shrineAltar = LevelGen.Rn2(shrines.Count);

    Log.Write($"shrine at altar {shrineAltar}");

    for (int i = 0; i < shrines.Count; i++)
    {
      if (i == shrineAltar)
      {
        b.Level.GetOrCreateState(shrines[i]).Feature = new("shrine");
      }
      else
      {
        b.Monster(WrongShrineGuardian, shrines[i]);
      }
    }

    // Everything is unlit!
    foreach (var room in b.Level.Rooms)
      room.Flags &= ~RoomFlags.Lit;

    var attendantCount = LevelGen.RnRange(3, 6);
    var bigRoom = b.Room(0);
    for (int i = 0; i < attendantCount; i++)
    {
      var pos = b.Context.FindLocationInRoom(bigRoom, p => lvl.UnitAt(p) == null);
      if (pos == null) break;
      b.Monster(ShrineAttendant, pos.Value);
    }
  })
  {
    Name = "The Shrine",
    HasStairsUp = true,
  };
}