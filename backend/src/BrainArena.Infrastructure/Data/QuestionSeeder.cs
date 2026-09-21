using BrainArena.Domain.Entities;
using BrainArena.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BrainArena.Infrastructure.Data;

/// <summary>
/// Seeds the original question bank: 20 questions per topic (Math, Geography, Chemistry,
/// ICFES-style General), written from scratch — none copied from exam-prep books or websites.
/// </summary>
public static class QuestionSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrainArenaDbContext>();

        if (await db.Questions.AnyAsync())
        {
            return;
        }

        db.Questions.AddRange(BuildQuestions());
        await db.SaveChangesAsync();
    }

    private static IEnumerable<Question> BuildQuestions()
    {
        return Math().Concat(Geography()).Concat(Chemistry()).Concat(IcfesGeneral());
    }

    private static Question Q(RoomTopic topic, string text, string[] options, int correct, string explanation) => new()
    {
        Id = Guid.NewGuid(),
        Topic = topic,
        Difficulty = 1,
        Text = text,
        Options = options,
        CorrectOptionIndex = correct,
        Explanation = explanation,
        Language = "es"
    };

    private static IEnumerable<Question> Math()
    {
        const RoomTopic t = RoomTopic.Math;
        yield return Q(t, "¿Cuánto es 7 × 8?", ["48", "54", "56", "64"], 2, "7 × 8 = 56.");
        yield return Q(t, "¿Cuánto es 15 + 27?", ["42", "32", "52", "40"], 0, "15 + 27 = 42.");
        yield return Q(t, "¿Cuál es la raíz cuadrada de 81?", ["8", "9", "7", "10"], 1, "9 × 9 = 81, así que √81 = 9.");
        yield return Q(t, "¿Cuánto es 100 ÷ 4?", ["20", "25", "30", "15"], 1, "100 ÷ 4 = 25.");
        yield return Q(t, "Un triángulo tiene ángulos de 60° y 70°. ¿Cuánto mide el tercer ángulo?", ["50°", "40°", "60°", "45°"], 0, "Los ángulos de un triángulo suman 180°: 180 − 60 − 70 = 50°.");
        yield return Q(t, "¿Cuál es el perímetro de un cuadrado de lado 6 cm?", ["36 cm", "24 cm", "12 cm", "18 cm"], 1, "El perímetro es 4 × lado = 4 × 6 = 24 cm.");
        yield return Q(t, "¿Cuánto es 9²?", ["81", "72", "90", "18"], 0, "9² = 9 × 9 = 81.");
        yield return Q(t, "¿Cuál de estas fracciones es equivalente a 1/2?", ["2/4", "3/8", "1/3", "2/5"], 0, "2/4 se simplifica dividiendo por 2, dando 1/2.");
        yield return Q(t, "¿Cuánto es 12 − 5 × 2?", ["2", "14", "-2", "24"], 0, "Primero se multiplica: 5 × 2 = 10, luego 12 − 10 = 2.");
        yield return Q(t, "¿Cuántos lados tiene un hexágono?", ["5", "6", "7", "8"], 1, "Un hexágono tiene 6 lados.");
        yield return Q(t, "¿Cuál es el área de un rectángulo de base 5 y altura 3?", ["15", "8", "20", "10"], 0, "El área es base × altura = 5 × 3 = 15.");
        yield return Q(t, "¿Cuál de estos números es primo?", ["21", "29", "33", "45"], 1, "29 solo es divisible por 1 y por sí mismo; los otros no lo son.");
        yield return Q(t, "¿Cuánto es 6 × 9?", ["45", "54", "56", "63"], 1, "6 × 9 = 54.");
        yield return Q(t, "¿Cuál es el resultado de 144 ÷ 12?", ["10", "11", "12", "13"], 2, "144 ÷ 12 = 12.");
        yield return Q(t, "¿Cuánto es 3³?", ["9", "27", "21", "18"], 1, "3³ = 3 × 3 × 3 = 27.");
        yield return Q(t, "Si x + 5 = 12, ¿cuánto vale x?", ["5", "6", "7", "8"], 2, "x = 12 − 5 = 7.");
        yield return Q(t, "¿Cuántos grados tiene un ángulo recto?", ["45°", "90°", "180°", "360°"], 1, "Un ángulo recto mide exactamente 90°.");
        yield return Q(t, "¿Cuál es el mínimo común múltiplo de 4 y 6?", ["10", "12", "24", "8"], 1, "12 es el menor número que es múltiplo de 4 y de 6 a la vez.");
        yield return Q(t, "¿Cuánto es 0.5 + 0.25?", ["0.65", "0.7", "0.75", "0.8"], 2, "0.5 + 0.25 = 0.75.");
        yield return Q(t, "¿Cuál es el volumen de un cubo de lado 3?", ["9", "18", "27", "81"], 2, "El volumen de un cubo es lado³ = 3³ = 27.");
    }

    private static IEnumerable<Question> Geography()
    {
        const RoomTopic t = RoomTopic.Geography;
        yield return Q(t, "¿Cuál es la capital de Colombia?", ["Medellín", "Bogotá", "Cali", "Cartagena"], 1, "Bogotá es la capital y ciudad más poblada de Colombia.");
        yield return Q(t, "¿Cuál es el río con mayor caudal del mundo?", ["Nilo", "Amazonas", "Misisipi", "Yangtsé"], 1, "El río Amazonas tiene el mayor caudal de agua del planeta.");
        yield return Q(t, "¿Cuál es el océano más grande?", ["Atlántico", "Índico", "Pacífico", "Ártico"], 2, "El océano Pacífico es el más extenso, cubriendo casi un tercio de la Tierra.");
        yield return Q(t, "¿Cuál es el continente más poblado?", ["África", "Asia", "Europa", "América"], 1, "Asia concentra más de la mitad de la población mundial.");
        yield return Q(t, "¿Cuál es el desierto más grande del mundo por superficie?", ["Sahara", "Gobi", "Antártico", "Kalahari"], 2, "El desierto antártico es el más extenso del mundo, aunque es frío en vez de cálido.");
        yield return Q(t, "¿Cuál es el país más grande del mundo por superficie?", ["China", "Canadá", "Rusia", "Brasil"], 2, "Rusia es el país con mayor superficie territorial del mundo.");
        yield return Q(t, "¿En qué continente está Egipto?", ["Asia", "África", "Europa", "Oceanía"], 1, "Egipto se ubica en el noreste de África.");
        yield return Q(t, "¿Cuál es la montaña más alta del mundo?", ["K2", "Everest", "Aconcagua", "Kilimanjaro"], 1, "El monte Everest, en el Himalaya, es la montaña más alta sobre el nivel del mar.");
        yield return Q(t, "¿Cuál es la capital de Francia?", ["Marsella", "Lyon", "París", "Niza"], 2, "París es la capital de Francia.");
        yield return Q(t, "¿Qué línea imaginaria divide la Tierra en hemisferio norte y sur?", ["Meridiano de Greenwich", "Ecuador", "Trópico de Cáncer", "Círculo Polar Ártico"], 1, "La línea del Ecuador divide la Tierra en los hemisferios norte y sur.");
        yield return Q(t, "¿Cuál de estos países no tiene salida al mar?", ["Perú", "Bolivia", "Chile", "Colombia"], 1, "Bolivia es un país sin costa marítima (mediterráneo).");
        yield return Q(t, "¿Cuál es la cordillera continental más larga del mundo?", ["Alpes", "Himalaya", "Andes", "Montañas Rocosas"], 2, "La cordillera de los Andes recorre gran parte de Sudamérica y es la más larga del mundo.");
        yield return Q(t, "¿Cuál es la capital de España?", ["Barcelona", "Madrid", "Sevilla", "Valencia"], 1, "Madrid es la capital de España.");
        yield return Q(t, "¿Cuál es el país más poblado del mundo?", ["India", "China", "Estados Unidos", "Indonesia"], 0, "India superó a China como el país más poblado del mundo.");
        yield return Q(t, "¿En qué continente se encuentra Brasil?", ["América del Norte", "América del Sur", "Europa", "África"], 1, "Brasil está en América del Sur.");
        yield return Q(t, "¿Cuál es la mayor masa de agua interior del mundo?", ["Lago Titicaca", "Mar Caspio", "Lago Victoria", "Lago Superior"], 1, "El Mar Caspio es el mayor lago/mar interior del mundo por superficie.");
        yield return Q(t, "¿Cuál es la capital de Japón?", ["Osaka", "Kioto", "Tokio", "Yokohama"], 2, "Tokio es la capital de Japón.");
        yield return Q(t, "¿Qué país tiene forma de bota en el mapa?", ["España", "Italia", "Grecia", "Portugal"], 1, "La península italiana tiene una forma característica de bota.");
        yield return Q(t, "¿Cuál es el estrecho que separa España de África?", ["Estrecho de Magallanes", "Estrecho de Gibraltar", "Canal de Panamá", "Estrecho de Bering"], 1, "El estrecho de Gibraltar separa la península ibérica del norte de África.");
        yield return Q(t, "¿Cuál es el río más largo de Sudamérica?", ["Río de la Plata", "Río Orinoco", "Río Amazonas", "Río Paraná"], 2, "El río Amazonas es el más largo y caudaloso de Sudamérica.");
    }

    private static IEnumerable<Question> Chemistry()
    {
        const RoomTopic t = RoomTopic.Chemistry;
        yield return Q(t, "¿Cuál es el símbolo químico del oro?", ["Ag", "Au", "Fe", "Pb"], 1, "El símbolo del oro es Au, del latín \"aurum\".");
        yield return Q(t, "¿Cuál es el gas más abundante en la atmósfera terrestre?", ["Oxígeno", "Dióxido de carbono", "Nitrógeno", "Hidrógeno"], 2, "El nitrógeno compone cerca del 78% de la atmósfera terrestre.");
        yield return Q(t, "¿Cuál es la fórmula química del agua?", ["CO2", "H2O", "O2", "NaCl"], 1, "El agua está formada por dos átomos de hidrógeno y uno de oxígeno: H2O.");
        yield return Q(t, "¿Cuántos protones tiene el átomo de hidrógeno?", ["0", "1", "2", "3"], 1, "El hidrógeno tiene número atómico 1, es decir, un protón.");
        yield return Q(t, "¿Qué tipo de enlace comparte electrones entre átomos?", ["Iónico", "Covalente", "Metálico", "Puente de hidrógeno"], 1, "En el enlace covalente los átomos comparten pares de electrones.");
        yield return Q(t, "¿Cuál es el pH aproximado del agua pura?", ["0", "7", "14", "10"], 1, "El agua pura es neutra, con un pH cercano a 7.");
        yield return Q(t, "¿Qué elemento tiene el símbolo \"Na\"?", ["Nitrógeno", "Sodio", "Níquel", "Neón"], 1, "El símbolo Na corresponde al sodio, del latín \"natrium\".");
        yield return Q(t, "¿Qué partícula subatómica tiene carga negativa?", ["Protón", "Neutrón", "Electrón", "Núcleo"], 2, "El electrón tiene carga negativa; el protón es positivo y el neutrón es neutro.");
        yield return Q(t, "¿Cómo se llama la tabla que organiza los elementos químicos?", ["Tabla periódica", "Tabla de Mendel", "Cuadro atómico", "Matriz química"], 0, "Los elementos se organizan en la tabla periódica según su número atómico.");
        yield return Q(t, "¿Cuál de estos elementos es un gas noble?", ["Cloro", "Helio", "Nitrógeno", "Hidrógeno"], 1, "El helio es un gas noble, poco reactivo y muy estable.");
        yield return Q(t, "¿Cómo se llama una reacción química que libera calor?", ["Endotérmica", "Exotérmica", "Catalítica", "Neutra"], 1, "Una reacción exotérmica libera energía en forma de calor al entorno.");
        yield return Q(t, "¿Qué estado de la materia tiene volumen definido pero forma variable?", ["Sólido", "Líquido", "Gaseoso", "Plasma"], 1, "Los líquidos mantienen su volumen pero toman la forma del recipiente que los contiene.");
        yield return Q(t, "¿Cuál es el símbolo químico del hierro?", ["Fe", "Ir", "Hi", "Fr"], 0, "El símbolo del hierro es Fe, del latín \"ferrum\".");
        yield return Q(t, "¿Cuántos electrones tiene un átomo neutro de carbono (número atómico 6)?", ["4", "6", "8", "12"], 1, "En un átomo neutro, el número de electrones es igual al número atómico: 6.");
        yield return Q(t, "¿Qué gas producen las plantas durante la fotosíntesis?", ["Dióxido de carbono", "Nitrógeno", "Oxígeno", "Hidrógeno"], 2, "Las plantas liberan oxígeno como producto de la fotosíntesis.");
        yield return Q(t, "¿Qué estado de la materia tiene forma y volumen definidos?", ["Sólido", "Líquido", "Gaseoso", "Plasma"], 0, "Los sólidos mantienen tanto su forma como su volumen.");
        yield return Q(t, "¿Qué es el ácido sulfúrico?", ["H2SO4", "HCl", "HNO3", "CH3COOH"], 0, "El ácido sulfúrico tiene la fórmula química H2SO4.");
        yield return Q(t, "¿Cuál es el símbolo químico del potasio?", ["Po", "Pt", "K", "P"], 2, "El símbolo del potasio es K, del latín \"kalium\".");
        yield return Q(t, "¿Cuál de estas opciones es una sustancia pura y no una mezcla?", ["Agua salada", "Aire", "Agua destilada", "Granito"], 2, "El agua destilada es una sustancia pura; las otras son mezclas de varios componentes.");
        yield return Q(t, "¿Qué instrumento de laboratorio se usa para medir volúmenes de líquido con precisión?", ["Mechero", "Probeta", "Pinza", "Espátula"], 1, "La probeta graduada se usa para medir volúmenes de líquidos con precisión.");
    }

    private static IEnumerable<Question> IcfesGeneral()
    {
        const RoomTopic t = RoomTopic.IcfesGeneral;
        yield return Q(t, "¿Cuál es el órgano principal del sistema circulatorio?", ["Pulmón", "Corazón", "Hígado", "Riñón"], 1, "El corazón bombea la sangre a través de todo el sistema circulatorio.");
        yield return Q(t, "Si todos los perros son mamíferos y Rex es un perro, ¿qué se puede concluir?", ["Rex es un mamífero", "Rex no es un mamífero", "Rex es un gato", "No se puede concluir nada"], 0, "Es un silogismo válido: si todos los perros son mamíferos y Rex es un perro, Rex es mamífero.");
        yield return Q(t, "¿Cuál de las siguientes es una fuente de energía renovable?", ["Carbón", "Petróleo", "Energía solar", "Gas natural"], 2, "La energía solar proviene de una fuente inagotable a escala humana: el sol.");
        yield return Q(t, "¿Qué poder del Estado se encarga de crear las leyes en una democracia?", ["Ejecutivo", "Legislativo", "Judicial", "Electoral"], 1, "El poder Legislativo es el encargado de redactar y aprobar las leyes.");
        yield return Q(t, "¿Cuál es el sinónimo más adecuado de la palabra \"veloz\"?", ["Lento", "Rápido", "Silencioso", "Fuerte"], 1, "\"Veloz\" significa que se mueve o actúa con rapidez.");
        yield return Q(t, "¿Qué instrumento se usa para medir la temperatura?", ["Barómetro", "Termómetro", "Altímetro", "Higrómetro"], 1, "El termómetro mide la temperatura; el barómetro mide presión atmosférica.");
        yield return Q(t, "¿Cuál de estas opciones es un derecho fundamental reconocido ampliamente?", ["La propiedad exclusiva de un idioma", "La libertad de expresión", "El derecho a no pagar impuestos", "El derecho a la nobleza"], 1, "La libertad de expresión es un derecho fundamental reconocido en la mayoría de constituciones.");
        yield return Q(t, "¿Qué proceso permite a las plantas producir su propio alimento usando luz solar?", ["Respiración", "Fotosíntesis", "Fermentación", "Digestión"], 1, "La fotosíntesis convierte luz solar, agua y CO2 en energía química para la planta.");
        yield return Q(t, "En un texto argumentativo, ¿qué elemento presenta la posición principal del autor?", ["La conclusión", "La tesis", "El pie de página", "El índice"], 1, "La tesis es la idea central que el autor defiende a lo largo del texto.");
        yield return Q(t, "¿Cuál de las siguientes unidades mide la energía eléctrica consumida?", ["Watt", "Kilovatio-hora", "Amperio", "Voltio"], 1, "El kilovatio-hora (kWh) mide la energía consumida a lo largo del tiempo.");
        yield return Q(t, "¿Qué significa la sigla \"ONU\"?", ["Organización Nacional Unida", "Organización de las Naciones Unidas", "Oficina Nacional de Urbanismo", "Organismo Nuclear Universal"], 1, "ONU significa Organización de las Naciones Unidas.");
        yield return Q(t, "Un tren sale a las 8:00 a.m. y tarda 2 horas y 30 minutos en llegar. ¿A qué hora llega?", ["10:00 a.m.", "10:30 a.m.", "11:00 a.m.", "9:30 a.m."], 1, "8:00 a.m. + 2 horas 30 minutos = 10:30 a.m.");
        yield return Q(t, "¿Cuál es el órgano encargado de filtrar la sangre y producir orina?", ["Hígado", "Riñón", "Pulmón", "Estómago"], 1, "Los riñones filtran la sangre y producen la orina.");
        yield return Q(t, "¿Cuál de estas palabras es un antónimo de \"abundante\"?", ["Escaso", "Numeroso", "Amplio", "Extenso"], 0, "\"Escaso\" significa poco o insuficiente, lo opuesto de \"abundante\".");
        yield return Q(t, "¿Qué figura literaria compara dos elementos usando la palabra \"como\"?", ["Metáfora", "Símil", "Hipérbole", "Personificación"], 1, "El símil compara explícitamente dos elementos usando un nexo comparativo como \"como\".");
        yield return Q(t, "¿Cuál es la función principal del poder Judicial?", ["Crear leyes", "Administrar justicia", "Gobernar el país", "Recaudar impuestos"], 1, "El poder Judicial se encarga de administrar justicia e interpretar las leyes.");
        yield return Q(t, "Un producto cuesta $40.000 y tiene un descuento del 25%. ¿Cuál es el precio final?", ["$10.000", "$30.000", "$35.000", "$32.000"], 1, "El 25% de 40.000 es 10.000; el precio final es 40.000 − 10.000 = 30.000.");
        yield return Q(t, "¿Qué gas es el principal responsable del efecto invernadero producido por la actividad humana?", ["Oxígeno", "Nitrógeno", "Dióxido de carbono", "Helio"], 2, "El dióxido de carbono (CO2) es el principal gas de efecto invernadero de origen humano.");
        yield return Q(t, "¿Cuál es el continente con más países?", ["Asia", "África", "Europa", "América"], 1, "África es el continente con más países, alrededor de 54.");
        yield return Q(t, "¿Qué tipo de texto tiene como propósito principal informar sobre hechos de forma objetiva?", ["Texto narrativo", "Texto argumentativo", "Texto informativo", "Texto poético"], 2, "El texto informativo busca comunicar hechos de manera objetiva y clara.");
    }
}
