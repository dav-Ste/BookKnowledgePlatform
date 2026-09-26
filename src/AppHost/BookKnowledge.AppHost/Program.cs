var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
    .WithDataVolume("bookknowledge-sql-data");

var database = sql.AddDatabase("bookknowledge");

var rabbit = builder.AddRabbitMQ("messaging")
    .WithDataVolume("bookknowledge-rabbit-data");

var qdrant = builder.AddQdrant("vectors")
    .WithDataVolume("bookknowledge-qdrant-data");

var identity = builder.AddProject<Projects.BookKnowledge_Identity_Api>("identity")
    .WithReference(database)
    .WithReference(rabbit)
    .WaitFor(database);

var books = builder.AddProject<Projects.BookKnowledge_Books_Api>("books")
    .WithReference(database)
    .WithReference(rabbit)
    .WaitFor(database);

var content = builder.AddProject<Projects.BookKnowledge_ContentSearch_Api>("content-search")
    .WithReference(database)
    .WithReference(qdrant)
    .WaitFor(database)
    .WaitFor(qdrant);

var indexing = builder.AddProject<Projects.BookKnowledge_Indexing_Worker>("indexing-worker")
    .WithReference(database)
    .WithReference(rabbit)
    .WithReference(qdrant)
    .WaitFor(database)
    .WaitFor(qdrant)
    .WaitFor(rabbit);

var gateway = builder.AddProject<Projects.BookKnowledge_Gateway>("gateway")
    .WithReference(identity)
    .WithReference(books)
    .WithReference(content)
    .WaitFor(identity)
    .WaitFor(books)
    .WaitFor(content);

var bookWeb = builder.AddProject<Projects.BookKnowledge_BookSearch_Web>("book-search-web")
    .WithReference(gateway)
    .WaitFor(gateway);

var contentWeb = builder.AddProject<Projects.BookKnowledge_ContentSearch_Web>("content-search-web")
    .WithReference(gateway)
    .WaitFor(gateway);

builder.Build().Run();
