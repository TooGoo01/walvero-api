using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationSchedulingAndBirthday : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "TenantNotificationLogs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Scope",
                table: "TenantNotificationLogs",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "AppSent",
                table: "TenantNotificationLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AudienceSummary",
                table: "TenantNotificationLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Body",
                table: "TenantNotificationLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Channels",
                table: "TenantNotificationLogs",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScheduledNotificationId",
                table: "TenantNotificationLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "TenantNotificationLogs",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "manual");

            migrationBuilder.AddColumn<int>(
                name: "WalletSent",
                table: "TenantNotificationLogs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BirthdayNotificationLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GiftAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LoyaltyCardId = table.Column<int>(type: "int", nullable: true),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BirthdayNotificationLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BonusGrants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    LoyaltyCardId = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExpiredAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BonusGrants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AudienceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ToApp = table.Column<bool>(type: "bit", nullable: false),
                    ToWallet = table.Column<bool>(type: "bit", nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecipientCount = table.Column<int>(type: "int", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BranchScopeId = table.Column<int>(type: "int", nullable: true),
                    IsImmediate = table.Column<bool>(type: "bit", nullable: false),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantBirthdaySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    SendHourLocal = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ToApp = table.Column<bool>(type: "bit", nullable: false),
                    ToWallet = table.Column<bool>(type: "bit", nullable: false),
                    GiftEnabled = table.Column<bool>(type: "bit", nullable: false),
                    GiftValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GiftExpiryDays = table.Column<int>(type: "int", nullable: true),
                    LastRunLocalDate = table.Column<DateTime>(type: "date", nullable: true),
                    RegUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RegDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EditUserId = table.Column<int>(type: "int", nullable: true),
                    EditDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantBirthdaySettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantNotificationLogs_TenantId_RegDate",
                table: "TenantNotificationLogs",
                columns: new[] { "TenantId", "RegDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BirthdayNotificationLogs_TenantId_CustomerId_Year",
                table: "BirthdayNotificationLogs",
                columns: new[] { "TenantId", "CustomerId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BonusGrants_LoyaltyCardId",
                table: "BonusGrants",
                column: "LoyaltyCardId");

            migrationBuilder.CreateIndex(
                name: "IX_BonusGrants_Status_ExpiresAtUtc",
                table: "BonusGrants",
                columns: new[] { "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledNotifications_Status_ScheduledAtUtc",
                table: "ScheduledNotifications",
                columns: new[] { "Status", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledNotifications_TenantId_ScheduledAtUtc",
                table: "ScheduledNotifications",
                columns: new[] { "TenantId", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantBirthdaySettings_TenantId",
                table: "TenantBirthdaySettings",
                column: "TenantId",
                unique: true);

            // ==== Vahid cins kodu (0=Kişi, 1=Qadın, 2=Digər) — data normallaşdırması (2026-10-02) ====
            // 1) PrimeHill importu (tenant 56) köhnə enum ilə yazmışdı (1=Kişi, 2=Qadın): 482 müştəri, yalnız dəyər hələ import kodudursa.
            migrationBuilder.Sql(@"
UPDATE c SET c.Gender = v.NewG
FROM Customers c
JOIN (VALUES
(987,2,1),
(1030,2,1),
(1069,1,0),
(1070,2,1),
(1071,2,1),
(1072,2,1),
(1073,2,1),
(1074,2,1),
(1075,2,1),
(1076,2,1),
(1077,2,1),
(1078,2,1),
(1079,1,0),
(1080,1,0),
(1081,2,1),
(1082,2,1),
(1083,2,1),
(1084,1,0),
(1085,2,1),
(1086,2,1),
(1087,2,1),
(1088,2,1),
(1089,2,1),
(1090,2,1),
(1091,2,1),
(1092,2,1),
(1093,2,1),
(1094,2,1),
(1095,2,1),
(1096,1,0),
(1097,1,0),
(1098,2,1),
(1099,2,1),
(1100,2,1),
(1101,1,0),
(1102,2,1),
(1103,2,1),
(1104,1,0),
(1105,2,1),
(1106,2,1),
(1107,1,0),
(1108,2,1),
(1109,2,1),
(1110,2,1),
(1111,2,1),
(1112,2,1),
(1113,2,1),
(1114,2,1),
(1115,2,1),
(1116,2,1),
(1117,2,1),
(1118,2,1),
(1119,2,1),
(1120,1,0),
(1121,2,1),
(1122,2,1),
(1123,2,1),
(1124,2,1),
(1125,2,1),
(1126,2,1),
(1127,2,1),
(1128,2,1),
(1129,2,1),
(1130,1,0),
(1131,1,0),
(1132,2,1),
(1133,1,0),
(1134,2,1),
(1135,2,1),
(1136,2,1),
(1137,2,1),
(1138,2,1),
(1139,2,1),
(1140,1,0),
(1141,2,1),
(1142,2,1),
(1143,1,0),
(1144,2,1),
(1146,1,0),
(1147,1,0),
(1148,1,0),
(1149,2,1),
(1150,2,1),
(1151,1,0),
(1152,1,0),
(1153,1,0),
(1154,1,0),
(1155,1,0),
(1156,2,1),
(1157,2,1),
(1158,2,1),
(1159,1,0),
(1160,2,1),
(1161,2,1),
(1162,1,0),
(1163,1,0),
(1164,2,1),
(1165,1,0),
(1166,2,1),
(1167,2,1),
(1168,2,1),
(1169,2,1),
(1170,1,0),
(1171,1,0),
(1172,2,1),
(1173,1,0),
(1174,2,1),
(1175,1,0),
(1176,2,1),
(1177,1,0),
(1178,2,1),
(1179,2,1),
(1180,2,1),
(1181,2,1),
(1182,2,1),
(1183,2,1),
(1184,2,1),
(1185,2,1),
(1186,2,1),
(1187,2,1),
(1188,2,1),
(1189,2,1),
(1190,2,1),
(1191,2,1),
(1192,2,1),
(1193,1,0),
(1194,1,0),
(1195,2,1),
(1196,1,0),
(1197,2,1),
(1198,1,0),
(1199,2,1),
(1200,2,1),
(1201,2,1),
(1202,2,1),
(1203,2,1),
(1204,1,0),
(1205,2,1),
(1206,1,0),
(1207,2,1),
(1208,1,0),
(1209,2,1),
(1210,1,0),
(1211,2,1),
(1212,1,0),
(1213,1,0),
(1214,1,0),
(1215,2,1),
(1216,2,1),
(1217,2,1),
(1218,2,1),
(1219,2,1),
(1220,1,0),
(1221,2,1),
(1222,2,1),
(1223,2,1),
(1224,1,0),
(1225,2,1),
(1226,2,1),
(1227,1,0),
(1228,1,0),
(1229,2,1),
(1230,1,0),
(1231,2,1),
(1232,2,1),
(1233,2,1),
(1234,2,1),
(1235,2,1),
(1236,2,1),
(1237,2,1),
(1238,2,1),
(1239,2,1),
(1240,2,1),
(1241,2,1),
(1242,2,1),
(1243,2,1),
(1244,1,0),
(1245,1,0),
(1246,1,0),
(1247,1,0),
(1248,2,1),
(1249,2,1),
(1250,2,1),
(1251,2,1),
(1252,2,1),
(1253,2,1),
(1254,2,1),
(1255,1,0),
(1256,2,1),
(1257,2,1),
(1258,2,1),
(1259,2,1),
(1260,2,1),
(1261,1,0),
(1262,1,0),
(1263,2,1),
(1264,2,1),
(1265,2,1),
(1266,1,0),
(1267,2,1),
(1268,1,0),
(1269,1,0),
(1270,1,0),
(1271,2,1),
(1272,2,1),
(1273,1,0),
(1274,2,1),
(1275,2,1),
(1276,1,0),
(1277,2,1),
(1278,1,0),
(1279,1,0),
(1280,2,1),
(1281,2,1),
(1282,2,1),
(1283,1,0),
(1284,2,1),
(1285,1,0),
(1286,1,0),
(1287,2,1),
(1288,1,0),
(1289,2,1),
(1290,2,1),
(1291,2,1),
(1292,1,0),
(1293,2,1),
(1294,2,1),
(1295,2,1),
(1296,1,0),
(1297,1,0),
(1298,1,0),
(1299,2,1),
(1300,2,1),
(1301,1,0),
(1302,1,0),
(1303,1,0),
(1304,2,1),
(1305,2,1),
(1306,1,0),
(1307,2,1),
(1308,1,0),
(1309,2,1),
(1310,1,0),
(1311,2,1),
(1312,1,0),
(1313,2,1),
(1314,2,1),
(1315,2,1),
(1316,2,1),
(1317,2,1),
(1318,2,1),
(1319,2,1),
(1320,2,1),
(1321,2,1),
(1322,2,1),
(1323,2,1),
(1324,1,0),
(1325,2,1),
(1326,1,0),
(1327,1,0),
(1328,2,1),
(1329,2,1),
(1330,1,0),
(1331,1,0),
(1332,2,1),
(1333,2,1),
(1334,1,0),
(1335,2,1),
(1336,1,0),
(1337,2,1),
(1338,2,1),
(1339,1,0),
(1340,1,0),
(1341,1,0),
(1342,1,0),
(1343,2,1),
(1344,2,1),
(1345,2,1),
(1346,2,1),
(1347,2,1),
(1348,2,1),
(1349,2,1),
(1350,1,0),
(1351,2,1),
(1352,2,1),
(1353,1,0),
(1354,2,1),
(1355,2,1),
(1356,1,0),
(1357,1,0),
(1358,1,0),
(1359,1,0),
(1360,2,1),
(1361,2,1),
(1362,1,0),
(1363,2,1),
(1364,1,0),
(1365,2,1),
(1366,1,0),
(1367,2,1),
(1368,2,1),
(1369,2,1),
(1370,2,1),
(1371,2,1),
(1372,2,1),
(1373,2,1),
(1374,2,1),
(1375,2,1),
(1376,1,0),
(1377,2,1),
(1378,2,1),
(1379,2,1),
(1380,2,1),
(1381,2,1),
(1382,2,1),
(1383,2,1),
(1384,2,1),
(1385,2,1),
(1386,1,0),
(1387,1,0),
(1388,2,1),
(1389,1,0),
(1390,1,0),
(1391,1,0),
(1392,2,1),
(1393,2,1),
(1394,1,0),
(1395,2,1),
(1396,1,0),
(1397,1,0),
(1398,2,1),
(1399,1,0),
(1400,2,1),
(1401,1,0),
(1402,1,0),
(1403,2,1),
(1404,1,0),
(1405,2,1),
(1406,1,0),
(1407,1,0),
(1408,1,0),
(1409,2,1),
(1410,1,0),
(1411,2,1),
(1412,1,0),
(1413,2,1),
(1414,1,0),
(1415,2,1),
(1416,1,0),
(1417,2,1),
(1418,2,1),
(1419,1,0),
(1420,2,1),
(1421,2,1),
(1422,1,0),
(1423,2,1),
(1424,2,1),
(1425,1,0),
(1426,2,1),
(1427,2,1),
(1428,2,1),
(1429,2,1),
(1430,2,1),
(1431,2,1),
(1432,2,1),
(1433,2,1),
(1434,2,1),
(1435,2,1),
(1436,2,1),
(1437,2,1),
(1438,2,1),
(1439,1,0),
(1440,1,0),
(1441,1,0),
(1442,1,0),
(1443,2,1),
(1444,1,0),
(1445,2,1),
(1446,1,0),
(1447,2,1),
(1448,1,0),
(1449,1,0),
(1450,1,0),
(1451,1,0),
(1452,2,1),
(1453,2,1),
(1454,2,1),
(1455,1,0),
(1456,1,0),
(1457,1,0),
(1458,1,0),
(1459,2,1),
(1460,1,0),
(1461,2,1),
(1462,2,1),
(1463,2,1),
(1464,2,1),
(1465,2,1),
(1466,2,1),
(1467,2,1),
(1468,1,0),
(1469,2,1),
(1470,2,1),
(1471,1,0),
(1472,2,1),
(1473,2,1),
(1474,2,1),
(1475,2,1),
(1476,2,1),
(1477,2,1),
(1478,2,1),
(1479,1,0),
(1480,2,1),
(1481,2,1),
(1482,2,1),
(1483,2,1),
(1484,2,1),
(1485,2,1),
(1486,2,1),
(1487,2,1),
(1488,2,1),
(1489,2,1),
(1490,2,1),
(1491,2,1),
(1492,2,1),
(1493,2,1),
(1494,2,1),
(1495,2,1),
(1496,2,1),
(1497,2,1),
(1498,2,1),
(1499,1,0),
(1500,2,1),
(1501,2,1),
(1502,2,1),
(1503,2,1),
(1504,2,1),
(1505,2,1),
(1506,2,1),
(1507,1,0),
(1508,1,0),
(1509,2,1),
(1510,1,0),
(1511,2,1),
(1512,1,0),
(1513,2,1),
(1514,2,1),
(1515,2,1),
(1516,2,1),
(1517,2,1),
(1518,1,0),
(1519,1,0),
(1520,1,0),
(1521,2,1),
(1522,1,0),
(1523,1,0),
(1524,2,1),
(1525,2,1),
(1526,2,1),
(1527,1,0),
(1528,2,1),
(1529,2,1),
(1530,2,1),
(1531,1,0),
(1532,1,0),
(1533,2,1),
(1534,2,1),
(1535,2,1),
(1537,2,1),
(1538,2,1),
(1539,2,1),
(1540,1,0),
(1541,1,0),
(1542,1,0),
(1543,2,1),
(1544,1,0),
(1545,2,1),
(1546,1,0),
(1547,2,1),
(1548,1,0),
(1549,2,1),
(1550,1,0)
) v(Id, OldG, NewG) ON c.Id = v.Id AND c.Gender = v.OldG;");

            // 2) 2026-04-11-dən əvvəl mənası bilinməyən '2' (PrimeHill xaric) → bilinmir (NULL)
            migrationBuilder.Sql(@"
UPDATE Customers SET Gender = NULL
WHERE Gender = 2 AND RegDate < '2026-04-11' AND Id NOT IN (987,1030,1069,1070,1071,1072,1073,1074,1075,1076,1077,1078,1079,1080,1081,1082,1083,1084,1085,1086,1087,1088,1089,1090,1091,1092,1093,1094,1095,1096,1097,1098,1099,1100,1101,1102,1103,1104,1105,1106,1107,1108,1109,1110,1111,1112,1113,1114,1115,1116,1117,1118,1119,1120,1121,1122,1123,1124,1125,1126,1127,1128,1129,1130,1131,1132,1133,1134,1135,1136,1137,1138,1139,1140,1141,1142,1143,1144,1146,1147,1148,1149,1150,1151,1152,1153,1154,1155,1156,1157,1158,1159,1160,1161,1162,1163,1164,1165,1166,1167,1168,1169,1170,1171,1172,1173,1174,1175,1176,1177,1178,1179,1180,1181,1182,1183,1184,1185,1186,1187,1188,1189,1190,1191,1192,1193,1194,1195,1196,1197,1198,1199,1200,1201,1202,1203,1204,1205,1206,1207,1208,1209,1210,1211,1212,1213,1214,1215,1216,1217,1218,1219,1220,1221,1222,1223,1224,1225,1226,1227,1228,1229,1230,1231,1232,1233,1234,1235,1236,1237,1238,1239,1240,1241,1242,1243,1244,1245,1246,1247,1248,1249,1250,1251,1252,1253,1254,1255,1256,1257,1258,1259,1260,1261,1262,1263,1264,1265,1266,1267,1268,1269,1270,1271,1272,1273,1274,1275,1276,1277,1278,1279,1280,1281,1282,1283,1284,1285,1286,1287,1288,1289,1290,1291,1292,1293,1294,1295,1296,1297,1298,1299,1300,1301,1302,1303,1304,1305,1306,1307,1308,1309,1310,1311,1312,1313,1314,1315,1316,1317,1318,1319,1320,1321,1322,1323,1324,1325,1326,1327,1328,1329,1330,1331,1332,1333,1334,1335,1336,1337,1338,1339,1340,1341,1342,1343,1344,1345,1346,1347,1348,1349,1350,1351,1352,1353,1354,1355,1356,1357,1358,1359,1360,1361,1362,1363,1364,1365,1366,1367,1368,1369,1370,1371,1372,1373,1374,1375,1376,1377,1378,1379,1380,1381,1382,1383,1384,1385,1386,1387,1388,1389,1390,1391,1392,1393,1394,1395,1396,1397,1398,1399,1400,1401,1402,1403,1404,1405,1406,1407,1408,1409,1410,1411,1412,1413,1414,1415,1416,1417,1418,1419,1420,1421,1422,1423,1424,1425,1426,1427,1428,1429,1430,1431,1432,1433,1434,1435,1436,1437,1438,1439,1440,1441,1442,1443,1444,1445,1446,1447,1448,1449,1450,1451,1452,1453,1454,1455,1456,1457,1458,1459,1460,1461,1462,1463,1464,1465,1466,1467,1468,1469,1470,1471,1472,1473,1474,1475,1476,1477,1478,1479,1480,1481,1482,1483,1484,1485,1486,1487,1488,1489,1490,1491,1492,1493,1494,1495,1496,1497,1498,1499,1500,1501,1502,1503,1504,1505,1506,1507,1508,1509,1510,1511,1512,1513,1514,1515,1516,1517,1518,1519,1520,1521,1522,1523,1524,1525,1526,1527,1528,1529,1530,1531,1532,1533,1534,1535,1537,1538,1539,1540,1541,1542,1543,1544,1545,1546,1547,1548,1549,1550);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BirthdayNotificationLogs");

            migrationBuilder.DropTable(
                name: "BonusGrants");

            migrationBuilder.DropTable(
                name: "ScheduledNotifications");

            migrationBuilder.DropTable(
                name: "TenantBirthdaySettings");

            migrationBuilder.DropIndex(
                name: "IX_TenantNotificationLogs_TenantId_RegDate",
                table: "TenantNotificationLogs");

            migrationBuilder.DropColumn(
                name: "AppSent",
                table: "TenantNotificationLogs");

            migrationBuilder.DropColumn(
                name: "AudienceSummary",
                table: "TenantNotificationLogs");

            migrationBuilder.DropColumn(
                name: "Body",
                table: "TenantNotificationLogs");

            migrationBuilder.DropColumn(
                name: "Channels",
                table: "TenantNotificationLogs");

            migrationBuilder.DropColumn(
                name: "ScheduledNotificationId",
                table: "TenantNotificationLogs");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "TenantNotificationLogs");

            migrationBuilder.DropColumn(
                name: "WalletSent",
                table: "TenantNotificationLogs");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "TenantNotificationLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Scope",
                table: "TenantNotificationLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);
        }
    }
}
