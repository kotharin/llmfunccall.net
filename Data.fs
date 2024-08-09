namespace VectorDB


module Data = 

    open System
    open System.Collections.Generic
    open Milvus.Client
    open OpenAI.Embeddings
    open Microsoft.SemanticKernel.Text
    let milvusClient = new MilvusClient("localhost", 19530, false,"financials" );
    
    let getOPAIEmbeddingsForLines key model (data:seq<string>) =
        let textChunks = TextChunker.SplitPlainTextParagraphs(data, 2000, 200)

        let ec = new EmbeddingClient(model, key)
        textChunks
        |> Seq.map(fun chunk ->
            chunk, ec.GenerateEmbedding(chunk).Value.Vector
        )
    
    let getOpenAIEmbeddings (key:string ) (model:string) (data:string) = 

        // chunk the data
        let textChunks = TextChunker.SplitPlainTextLines(data, 200)
        
        let ec = new EmbeddingClient(model, key)
        
        textChunks
        |> Seq.map(fun chunk ->
            chunk, ec.GenerateEmbedding(chunk).Value.Vector
        )
    
    let createFinancialsCollection() =
        let cs = new CollectionSchema()
        cs.Fields.Add(FieldSchema.Create<int64>("companyId", true))
        cs.Fields.Add(FieldSchema.CreateVarchar("cik", 20))
        cs.Fields.Add(FieldSchema.CreateVarchar("chunk", 1000))
        cs.Fields.Add(FieldSchema.CreateFloatVector("notes", 1536, "Notes for the company that may contain legal and risk information"))
        
        milvusClient.CreateCollectionAsync("CompanyData",cs,ConsistencyLevel.Strong) |> Async.AwaitTask

    let createDatabase (db:string) =
        milvusClient.CreateDatabaseAsync(db) |> Async.AwaitTask

    let getDatabases() =
        milvusClient.ListDatabasesAsync() |> Async.AwaitTask
    
    let getCollections() =
        milvusClient.ListCollectionsAsync() |> Async.AwaitTask

    let insertData (key:string) (embeddingModel:string) (collectionName:string) (companyId:int64) (cik:string) (chunk:string) =
        let collection = milvusClient.GetCollection(collectionName)

        let vIds, vCiks, vChunks, vNotes =
            getOpenAIEmbeddings key embeddingModel chunk
            |> Seq.fold(fun (ids, ciks, chunks, notes) (textChunk, embedding) ->
                let newIds = Array.append ids [|companyId|]
                let newCiks = Array.append ciks [|cik|]
                let newChunks = Array.append chunks [|textChunk|]
                let newNotes = Array.append notes [|embedding|]
                newIds, newCiks, newChunks, newNotes
            ) (Array.empty, Array.empty, Array.empty, Array.empty)
        let (fieldsData:FieldData[]) = [|
            //FieldData.Create("companyId", vIds);
            FieldData.Create("cik", vCiks);
            FieldData.Create("chunk", vChunks);
            FieldData.CreateFloatVector("notes", vNotes);
        |]

        collection.InsertAsync(fieldsData) |> Async.AwaitTask

    let deleteDatabase(name:string) = 
        milvusClient.DropDatabaseAsync(name) |> Async.AwaitTask

    let createIndex (collectionName:string) = 
        let collection = milvusClient.GetCollection(collectionName)

        collection.CreateIndexAsync("notes" ,IndexType.Flat, SimilarityMetricType.Cosine, "notes_idx") |> Async.AwaitTask

    let search (key:string) (embeddingModel:string) (collectionName:string) (query:string) = task {
        let collection = milvusClient.GetCollection(collectionName)

        let! _ =  collection.LoadAsync()
        let! _ = collection.WaitForCollectionLoadAsync()

        // get the embeddings for the query
        let qe = getOpenAIEmbeddings key embeddingModel query
        
        let emb = 
            qe
            |> Seq.map snd
            |> Seq.toList

        let (readOnly:IReadOnlyList<ReadOnlyMemory<float32>>) = emb
        
        let searchParameters = new SearchParameters()
        searchParameters.OutputFields.Add("chunk")
        searchParameters.ConsistencyLevel <- ConsistencyLevel.Strong

        let! results = collection.SearchAsync("notes", emb, SimilarityMetricType.Cosine, 3, searchParameters)

        // extract the scores and data
        let chunkData = (results.FieldsData.Item(0)) :?> FieldData<string>

        // return the chunks and scores
        return
            chunkData.Data
            |> Seq.mapi(fun i fd ->
                results.Scores.Item(i), fd

            )
    }